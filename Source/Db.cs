
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Web;

namespace PaperclipPerfector
{
    // The lock on activePosts is absolutely more aggressive than necessary, but it really *really* does not matter for this application.
    public class Db
    {
        private SQLiteConnection dbConnection;

        private SQLiteCommand insertPost;
        private SQLiteCommand insertReportType;
        private SQLiteCommand clearReports;
        private SQLiteCommand insertReport;

        private SQLiteCommand updatePostState;

        private SQLiteCommand readPost;
        private SQLiteCommand readPosts;
        private SQLiteCommand readReportsFor;

        private SQLiteCommand updateReportType;

        private SQLiteCommand readReportType;
        private SQLiteCommand readReportTypes;
        private SQLiteCommand readUnassignedReportTypes;

        private HashSet<Action> callbacks = new HashSet<Action>();
        private Dictionary<string, WeakReference<Post>> activePosts = new Dictionary<string, WeakReference<Post>>();
        private Dictionary<string, WeakReference<ReportType>> activeReportTypes = new Dictionary<string, WeakReference<ReportType>>();

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
        }

        public class Post
        {
            public string id;
            public string author;
            public long ups;
            public string html;
            public string link;
            public string title;
            public DateTimeOffset creation;
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
            dbConnection = new SQLiteConnection("Data Source=db.sqlite");
            dbConnection.Open();

            // Init rows
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS posts (id TEXT PRIMARY KEY, author TEXT NOT NULL, html TEXT NOT NULL, ups INTEGER NOT NULL, permalink TEXT NOT NULL, timestamp INTEGER NOT NULL, title TEXT NOT NULL, state TEXT NOT NULL)");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS reportTypes (id TEXT PRIMARY KEY, category TEXT NOT NULL)");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS reports (postId TEXT NOT NULL, reportTypeId TEXT NOT NULL, count INTEGER NOT NULL, PRIMARY KEY(postId, reportTypeId))");

            // Init commands
            insertPost = new SQLiteCommand("INSERT INTO posts(id, author, html, ups, permalink, timestamp, title, state) VALUES(@id, @author, @html, @ups, @permalink, @timestamp, @title, @state) ON CONFLICT(id) DO UPDATE SET html=excluded.html, ups=excluded.ups", dbConnection);
            insertReportType = new SQLiteCommand($"INSERT OR IGNORE INTO reportTypes(id, category) VALUES(@id, '{ReportCategory.Unassigned}')", dbConnection);
            clearReports = new SQLiteCommand("DELETE FROM reports WHERE postId = @postId", dbConnection);
            insertReport = new SQLiteCommand("INSERT INTO reports(postId, reportTypeId, count) VALUES(@postId, @reportTypeId, @count)", dbConnection);

            updatePostState = new SQLiteCommand("UPDATE posts SET state = @state WHERE id = @id", dbConnection);

            readPost = new SQLiteCommand("SELECT id, author, html, ups, permalink, timestamp, title, state FROM posts WHERE id = @id", dbConnection);
            readPosts = new SQLiteCommand("SELECT DISTINCT posts.id AS id, author, html, ups, permalink, timestamp, title, state FROM posts INNER JOIN reports ON posts.id = reports.postId INNER JOIN reportTypes ON reports.reportTypeId = reportTypes.id WHERE state = @state AND reportTypes.category = 'Positive'", dbConnection);
            readReportsFor = new SQLiteCommand("SELECT reportTypeId, count FROM reports WHERE postId = @postId", dbConnection);

            updateReportType = new SQLiteCommand("UPDATE reportTypes SET category = @category WHERE id = @id", dbConnection);

            readReportType = new SQLiteCommand("SELECT id, category FROM reportTypes WHERE id = @id", dbConnection);
            readReportTypes = new SQLiteCommand("SELECT id, category FROM reportTypes", dbConnection);
            readUnassignedReportTypes = new SQLiteCommand("SELECT id, category FROM reportTypes WHERE category = 'Unassigned'", dbConnection);
        }

        public void RegisterCallback(Action callback)
        {
            callbacks.Add(callback);
        }

        public void UnregisterCallback(Action callback)
        {
            callbacks.Remove(callback);
        }

        private void TriggerCallbacks()
        {
            foreach (var callback in callbacks)
            {
                callback.Invoke();
            }
        }

        public void UpdatePostData(RedditApi.Post post)
        {
            lock (activePosts)
            {
                using (var transaction = dbConnection.BeginTransaction())
                {
                    insertPost.ExecuteNonQuery(new Dictionary<string, object>()
                    {
                        ["id"] = post.name,
                        ["author"] = post.author,
                        ["html"] = post.body_html ?? $"<a href=\"{HttpUtility.JavaScriptStringEncode(post.url)}\">{HttpUtility.HtmlEncode(post.url)}</a>",
                        ["ups"] = post.ups,
                        ["permalink"] = post.permalink,
                        ["timestamp"] = post.created_utc,
                        ["title"] = post.link_title ?? "",
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
        }

        public Post[] ReadAllPosts(PostState state)
        {
            lock (activePosts)
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
                        string id = posts.GetField<string>("id");
                        var post = activePosts.TryGetValue(id)?.TryGetTarget();

                        if (post == null)
                        {
                            post = new Post();
                            activePosts[id] = new WeakReference<Post>(post);

                            // If post isn't null, we already have the right data
                            ReadPostFromReader(posts, post);
                        }

                        result.Add(post);
                    }
                    posts.Close();

                    transaction.Rollback();

                    return result.ToArray();
                }
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
            post.ups = reader.GetField<long>("ups");
            post.link = reader.GetField<string>("permalink");
            post.title = reader.GetField<string>("title");
            post.creation = DateTimeOffset.FromUnixTimeSeconds(reader.GetField<long>("timestamp"));
            post.state = Util.EnumParse<PostState>(reader.GetField<string>("state"));

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
            lock (activePosts)
            {
                updatePostState.ExecuteNonQuery(new Dictionary<string, object>()
                {
                    ["id"] = post.id,
                    ["state"] = state.ToString(),
                });

                post.state = state;

                TriggerCallbacks();
            }
        }

        public ReportType GetReportType(string id)
        {
            // It's the wrong lock, but I don't want to worry about lock ordering. This is safe, just unnecessarily slow.
            lock (activePosts)
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
        }

        public ReportType[] ReadAllReportTypes()
        {
            // It's the wrong lock, but I don't want to worry about lock ordering. This is safe, just unnecessarily slow.
            lock (activePosts)
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
        }

        private void ReadReportTypeFromReader(SQLiteDataReader reader, ReportType reportType)
        {
            reportType.id = reader.GetField<string>("id");
            reportType.category = Util.EnumParse<ReportCategory>(reader.GetField<string>("category"));
        }

        public void UpdateReportTypeCategory(ReportType reportType, ReportCategory category)
        {
            // It's the wrong lock, but I don't want to worry about lock ordering. This is safe, just unnecessarily slow.
            lock (activePosts)
            {
                updateReportType.ExecuteNonQuery(new Dictionary<string, object>()
                {
                    ["id"] = reportType.id,
                    ["category"] = category.ToString(),
                });

                reportType.category = category;

                TriggerCallbacks();
            }
        }

        public bool HasUnassignedReportTypes()
        {
            var reader = readUnassignedReportTypes.ExecuteReader();
            bool result = reader.Read();
            reader.Close();
            return result;
        }
    }
}
