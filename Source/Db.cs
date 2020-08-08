
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace PaperclipPerfector
{
    // dbLock is absolutely more aggressive than necessary, but it really *really* does not matter for this application.
    public class Db
    {
        private SQLiteConnection dbConnection;

        private SQLiteCommand insertPost;
        private SQLiteCommand insertReportType;
        private SQLiteCommand clearReports;
        private SQLiteCommand insertReport;

        private SQLiteCommand updatePostState;
        private SQLiteCommand updateFlavorTitle;

        private SQLiteCommand readPost;
        private SQLiteCommand readPosts;
        private SQLiteCommand readReportsFor;

        private SQLiteCommand finalizePostUpdate;
        private SQLiteCommand finalizePostCommit;
        private SQLiteCommand updateReportType;

        private SQLiteCommand readReportType;
        private SQLiteCommand readReportTypes;
        private SQLiteCommand readUnassignedReportTypes;

        private SQLiteCommand readGlobalProp;
        private SQLiteCommand writeGlobalProp;

        // bool exists just because what we really want is a hashset
        // these are the only mutable values in here, the rest are all const and threadsafe
        private ConcurrentDictionary<Action, bool> callbacks = new ConcurrentDictionary<Action, bool>();
        private ConcurrentDictionary<string, WeakReference<Post>> activePosts = new ConcurrentDictionary<string, WeakReference<Post>>();
        private ConcurrentDictionary<string, WeakReference<ReportType>> activeReportTypes = new ConcurrentDictionary<string, WeakReference<ReportType>>();

        private SemaphoreSlim dbLock = new SemaphoreSlim(1, 1);

        private static Db StoredInstance;
        public static Db Instance
        {
            get
            {
                if (StoredInstance == null)
                {
                    StoredInstance = new Db();
                }

                return StoredInstance;
            }
        }

        public enum PostState
        {
            Pending,
            Approved,
            Rejected,
            Posted,
        }

        public class Post
        {
            public string id;
            public string author;
            public long ups;
            public string html;
            public string text;
            public string link;
            public string title;
            public DateTimeOffset creation;

            public string flavorTitle;
            public PostState state;

            public Report[] reports;

            public class Report
            {
                public ReportType reason;
                public long count;
            }

            public long ReportsOfCategory(ReportCategory category)
            {
                long count = 0;
                for (int i = 0; i < reports.Length; ++i)
                {
                    if (reports[i].reason.category == category)
                    {
                        count += reports[i].count;
                    }
                }
                return count;
            }
        }

        public enum ReportCategory
        {
            Unassigned,
            Positive,
            Neutral,
            Negative,
        }

        public class ReportType
        {
            public string id;
            public ReportCategory category;
        }

        public Db()
        {
            // Init DB
            dbConnection = new SQLiteConnection($"Data Source={Path.Join(Config.Datamount, "db.sqlite")}");
            dbConnection.Open();

            // Init rows
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS posts (id TEXT PRIMARY KEY, author TEXT NOT NULL, html TEXT NOT NULL, text TEXT NOT NULL, ups INTEGER NOT NULL, permalink TEXT NOT NULL, timestamp INTEGER NOT NULL, title TEXT NOT NULL, state TEXT NOT NULL)");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS reportTypes (id TEXT PRIMARY KEY, category TEXT NOT NULL)");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS reports (postId TEXT NOT NULL, reportTypeId TEXT NOT NULL, count INTEGER NOT NULL, PRIMARY KEY(postId, reportTypeId))");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS globalProps (key TEXT PRIMARY KEY, value TEXT NOT NULL)");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS postChunks (timestamp TEXT NOT NULL, id TEXT NOT NULL)");

            // Init commands
            insertPost = new SQLiteCommand("INSERT INTO posts(id, author, html, text, ups, permalink, timestamp, title, state) VALUES(@id, @author, @html, @text, @ups, @permalink, @timestamp, @title, @state) ON CONFLICT(id) DO UPDATE SET html=excluded.html, text=excluded.text, ups=excluded.ups, title=excluded.title", dbConnection);   // Note: The title *shouldn't* change, but at one point I had a bug where it wasn't parsed properly. This updates it for existing posts.
            insertReportType = new SQLiteCommand($"INSERT OR IGNORE INTO reportTypes(id, category) VALUES(@id, '{ReportCategory.Unassigned}')", dbConnection);
            clearReports = new SQLiteCommand("DELETE FROM reports WHERE postId = @postId", dbConnection);
            insertReport = new SQLiteCommand("INSERT INTO reports(postId, reportTypeId, count) VALUES(@postId, @reportTypeId, @count)", dbConnection);

            updatePostState = new SQLiteCommand("UPDATE posts SET state = @state WHERE id = @id", dbConnection);
            updateFlavorTitle = new SQLiteCommand("UPDATE posts SET flavorTitle = @flavorTitle WHERE id = @id", dbConnection);

            readPost = new SQLiteCommand("SELECT id, author, html, text, ups, permalink, timestamp, title, state, flavorTitle FROM posts WHERE id = @id", dbConnection);
            readPosts = new SQLiteCommand("SELECT DISTINCT posts.id AS id, author, html, text, ups, permalink, timestamp, title, state, flavorTitle FROM posts INNER JOIN reports ON posts.id = reports.postId INNER JOIN reportTypes ON reports.reportTypeId = reportTypes.id WHERE state = @state AND reportTypes.category = 'Positive' ORDER BY timestamp DESC", dbConnection);
            readReportsFor = new SQLiteCommand("SELECT reportTypeId, count FROM reports WHERE postId = @postId", dbConnection);

            finalizePostUpdate = new SQLiteCommand("UPDATE posts SET state = 'Posted' WHERE id = @id AND state = 'Approved'", dbConnection);
            finalizePostCommit = new SQLiteCommand("INSERT INTO postChunks(timestamp, id) VALUES(@timestamp, @id)", dbConnection);
            updateReportType = new SQLiteCommand("UPDATE reportTypes SET category = @category WHERE id = @id", dbConnection);

            readReportType = new SQLiteCommand("SELECT id, category FROM reportTypes WHERE id = @id", dbConnection);
            readReportTypes = new SQLiteCommand("SELECT id, category FROM reportTypes", dbConnection);
            readUnassignedReportTypes = new SQLiteCommand("SELECT id, category FROM reportTypes WHERE category = 'Unassigned'", dbConnection);

            readGlobalProp = new SQLiteCommand("SELECT value FROM globalProps WHERE key = @key", dbConnection);
            writeGlobalProp = new SQLiteCommand("INSERT INTO globalProps(key, value) VALUES(@key, @value) ON CONFLICT(key) DO UPDATE SET value=excluded.value", dbConnection);
        }

        public void UpdateSchema()
        {
            if (GlobalProps.Instance.dbVersion.Value == 1)
            {
                // Update to v2 - add a flavorTitle field to posts
                dbConnection.ExecuteNonQuery("ALTER TABLE posts ADD flavorTitle NOT NULL DEFAULT ''");
                GlobalProps.Instance.dbVersion.Value = 2;
            }

            if (GlobalProps.Instance.dbVersion.Value != 2)
            {
                Dbg.Err("Database error! Something is very wrong!");
            }
        }

        public void RegisterCallback(Action callback)
        {
            callbacks.TryAdd(callback, true);
        }

        public void UnregisterCallback(Action callback)
        {
            callbacks.Remove(callback, out _);
        }

        private void TriggerCallbacks()
        {
            foreach (var callback in callbacks)
            {
                callback.Key.Invoke();
            }
        }

        public void UpdatePostData(RedditApi.Post post)
        {
            dbLock.Wait();

            try
            {
                using (var transaction = dbConnection.BeginTransaction())
                {
                    insertPost.ExecuteNonQuery(new Dictionary<string, object>()
                    {
                        ["id"] = post.name,
                        ["author"] = post.author,
                        ["html"] = post.body_html ?? $"<a href=\"{HttpUtility.JavaScriptStringEncode(post.url)}\">{HttpUtility.HtmlEncode(post.url)}</a>",
                        ["text"] = post.body ?? post.url,
                        ["ups"] = post.ups,
                        ["permalink"] = post.permalink,
                        ["timestamp"] = post.created_utc,
                        ["title"] = post.Title().Result,
                        ["state"] = PostState.Pending.ToString(),
                    });

                    clearReports.ExecuteNonQuery(new Dictionary<string, object>()
                    {
                        ["postId"] = post.name,
                    });

                    foreach (var report in post.Reports)
                    {
                        insertReportType.ExecuteNonQuery(new Dictionary<string, object>()
                        {
                            ["id"] = report.reason,
                        });

                        insertReport.ExecuteNonQuery(new Dictionary<string, object>()
                        {
                            ["postId"] = post.name,
                            ["reportTypeId"] = report.reason,
                            ["count"] = report.count,
                        });
                    }

                    // if this fails, it's OK, we'll just pick it up again on our next pass
                    transaction.Commit();
                }

                UpdateActivePost(post.name);
            }
            finally
            {
                dbLock.Release();
            }
        }

        public Post[] ReadAllPosts(PostState state)
        {
            // This is just for the sake of getting an atomic snapshot
            // It's okay if it's a little out of date
            using (var transaction = dbConnection.BeginTransaction())
            {
                var result = new List<Post>();

                var posts = readPosts.ExecuteReader(new Dictionary<string, object>()
                {
                    ["state"] = state.ToString(),
                });
                while (posts.Read())
                {
                    while (true)
                    {
                        string id = posts.GetField<string>("id");

                        Post post = null;
                        var oldRef = activePosts.GetOrAdd(id, id =>
                        {
                            post = new Post();

                            // If post isn't null, we already have the right data
                            ReadPostFromReader(posts, post);

                            return new WeakReference<Post>(post);
                        });

                        post = post ?? oldRef.TryGetTarget();

                        if (post == null)
                        {
                            // We didn't insert our object, but we also didn't get a valid object
                            // This suggests that we got a WeakReference without a valid pointer
                            // That's OK; (try to) remove it and try again.
                            activePosts.Remove(id, oldRef);
                            continue;
                        }

                        // Success!
                        result.Add(post);
                        break;
                    }
                }
                posts.Close();

                transaction.Rollback();

                return result.ToArray();
            }
        }

        private void UpdateActivePost(string id)
        {
            var post = activePosts.TryGetValue(id)?.TryGetTarget();

            if (post == null)
            {
                // nothin' to do
                return;
            }

            var postDb = readPost.ExecuteReader(new Dictionary<string, object>()
            {
                ["id"] = id,
            });
            postDb.Read();

            ReadPostFromReader(postDb, post);

            postDb.Close();

            TriggerCallbacks();
        }

        private void ReadPostFromReader(SQLiteDataReader reader, Post post)
        {
            post.id = reader.GetField<string>("id");
            post.author = reader.GetField<string>("author");
            post.html = reader.GetField<string>("html");
            post.text = reader.GetField<string>("text");
            post.ups = reader.GetField<long>("ups");
            post.link = reader.GetField<string>("permalink");
            post.title = reader.GetField<string>("title");
            
            post.creation = DateTimeOffset.FromUnixTimeSeconds(reader.GetField<long>("timestamp"));
            post.state = Util.EnumParse<PostState>(reader.GetField<string>("state"));
            post.flavorTitle = reader.GetField<string>("flavorTitle");

            var reports = new List<Post.Report>();
            var reportReader = readReportsFor.ExecuteReader(new Dictionary<string, object>()
            {
                ["postId"] = post.id,
            });
            while (reportReader.Read())
            {
                reports.Add(new Post.Report
                {
                    reason = GetReportType(reportReader.GetField<string>("reportTypeId")),
                    count = reportReader.GetField<long>("count"),
                });
            }
            reportReader.Close();

            post.reports = reports.OrderBy(report => report.count).ToArray();
        }

        public void UpdatePostState(Post post, PostState state)
        {
            updatePostState.ExecuteNonQuery(new Dictionary<string, object>()
            {
                ["id"] = post.id,
                ["state"] = state.ToString(),
            });

            post.state = state;

            TriggerCallbacks();
        }

        public void UpdateFlavorTitle(Post post, string flavorTitle)
        {
            updateFlavorTitle.ExecuteNonQuery(new Dictionary<string, object>()
            {
                ["id"] = post.id,
                ["flavorTitle"] = flavorTitle,
            });

            post.flavorTitle = flavorTitle;

            TriggerCallbacks();
        }

        public ReportType GetReportType(string id)
        {
            // It's the wrong lock, but I don't want to worry about lock ordering. This is safe, just unnecessarily slow.
            dbLock.Wait();

            try
            {
                var reportType = activeReportTypes.TryGetValue(id)?.TryGetTarget();
                if (reportType == null)
                {
                    reportType = new ReportType();
                    activeReportTypes[id] = new WeakReference<ReportType>(reportType);

                    var reader = readReportType.ExecuteReader(new Dictionary<string, object>()
                    {
                        ["id"] = id,
                    });
                    reader.Read();

                    ReadReportTypeFromReader(reader, reportType);

                    reader.Close();
                }

                return reportType;
            }
            finally
            {
                dbLock.Release();
            }
        }

        public ReportType[] ReadAllReportTypes()
        {
            // It's the wrong lock, but I don't want to worry about lock ordering. This is safe, just unnecessarily slow.
            dbLock.Wait();

            try
            {
                var result = new List<ReportType>();

                var reportTypes = readReportTypes.ExecuteReader();
                while (reportTypes.Read())
                {
                    string id = reportTypes.GetField<string>("id");
                    var reportType = activeReportTypes.TryGetValue(id)?.TryGetTarget();

                    if (reportType == null)
                    {
                        reportType = new ReportType();
                        activeReportTypes[id] = new WeakReference<ReportType>(reportType);

                        ReadReportTypeFromReader(reportTypes, reportType);
                    }

                    result.Add(reportType);
                }
                reportTypes.Close();

                return result.OrderBy(rt => rt.id).OrderBy(rt => rt.category).ToArray();
            }
            finally
            {
                dbLock.Release();
            }
        }

        private void ReadReportTypeFromReader(SQLiteDataReader reader, ReportType reportType)
        {
            reportType.id = reader.GetField<string>("id");
            reportType.category = Util.EnumParse<ReportCategory>(reader.GetField<string>("category"));
        }

        public void UpdateReportTypeCategory(ReportType reportType, ReportCategory category)
        {
            // It's the wrong lock, but I don't want to worry about lock ordering. This is safe, just unnecessarily slow.
            dbLock.Wait();

            try
            {
                updateReportType.ExecuteNonQuery(new Dictionary<string, object>()
                {
                    ["id"] = reportType.id,
                    ["category"] = category.ToString(),
                });

                reportType.category = category;

                TriggerCallbacks();
            }
            finally
            {
                dbLock.Release();
            }
        }

        public bool HasUnassignedReportTypes()
        {
            var reader = readUnassignedReportTypes.ExecuteReader();
            bool result = reader.Read();
            reader.Close();
            return result;
        }

        public string ReadGlobalProp(string key)
        {
            var reader = readGlobalProp.ExecuteReader(new Dictionary<string, object>
            {
                ["key"] = key,
            });

            string result = null;
            if (reader.Read())
            {
                result = reader.GetField<string>("value");
            }

            reader.Close();

            return result;
        }

        public void WriteGlobalProp(string key, string value)
        {
            writeGlobalProp.ExecuteNonQuery(new Dictionary<string, object>
            {
                ["key"] = key,
                ["value"] = value,
            });
        }

        public bool MoveToPosted(IEnumerable<string> ids, DateTimeOffset timestamp)
        {
            using (var transaction = dbConnection.BeginTransaction())
            {
                bool failed = false;
                foreach (var id in ids)
                {
                    using (var updated = finalizePostUpdate.ExecuteReader(new Dictionary<string, object> { ["id"] = id }))
                    {
                        if (updated.RecordsAffected != 1)
                        {
                            failed = true;

                            break;
                        }
                    }

                    finalizePostCommit.ExecuteNonQuery(new Dictionary<string, object> { ["id"] = id, ["timestamp"] = timestamp.ToUnixTimeSeconds() });
                }

                if (failed)
                {
                    transaction.Rollback();
                    return false;
                }

                transaction.Commit();
            }

            return true;
        }
    }
}
