
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace PaperclipPerfector
{
    // TODO: this all desperately needs to be properly synchronized
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

        private HashSet<Action> callbacks = new HashSet<Action>();
        private Dictionary<string, WeakReference<Post>> activePosts = new Dictionary<string, WeakReference<Post>>();

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
                public string reason;
                public long count;
            }
        }

        public enum PostState
        {
            Pending,
            Approved,
            Rejected,
        }

        public Db()
        {
            // Init DB
            dbConnection = new SQLiteConnection("Data Source=db.sqlite");
            dbConnection.Open();

            // Init rows
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS posts (id TEXT PRIMARY KEY, author TEXT, html TEXT, ups INTEGER, permalink TEXT, timestamp INTEGER, title TEXT, state TEXT)");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS reportTypes (id TEXT PRIMARY KEY, assigned INTEGER, value INTEGER)");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS reports (postId TEXT, reportTypeId TEXT, count INTEGER, PRIMARY KEY(postId, reportTypeId))");

            // Init commands
            insertPost = new SQLiteCommand("INSERT INTO posts(id, author, html, ups, permalink, timestamp, title, state) VALUES(@id, @author, @html, @ups, @permalink, @timestamp, @title, @state) ON CONFLICT(id) DO UPDATE SET html=excluded.html, ups=excluded.ups", dbConnection);
            insertReportType = new SQLiteCommand("INSERT OR IGNORE INTO reportTypes(id, assigned, value) VALUES(@id, 0, 0)", dbConnection);
            clearReports = new SQLiteCommand("DELETE FROM reports WHERE postId = @postId", dbConnection);
            insertReport = new SQLiteCommand("INSERT INTO reports(postId, reportTypeId, count) VALUES(@postId, @reportTypeId, @count)", dbConnection);

            updatePostState = new SQLiteCommand("UPDATE posts SET state = @state WHERE id = @id", dbConnection);

            readPost = new SQLiteCommand("SELECT id, author, html, ups, permalink, timestamp, title, state FROM posts WHERE id = @id", dbConnection);
            readPosts = new SQLiteCommand("SELECT id, author, html, ups, permalink, timestamp, title, state FROM posts WHERE state = @state", dbConnection);
            readReportsFor = new SQLiteCommand("SELECT reportTypeId, count FROM reports WHERE postId = @postId", dbConnection);
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
            using (var transaction = dbConnection.BeginTransaction())
            {
                insertPost.ExecuteNonQuery(new Dictionary<string, object>()
                {
                    ["id"] = post.id,
                    ["author"] = post.author,
                    ["html"] = post.body_html,
                    ["ups"] = post.ups,
                    ["permalink"] = post.permalink,
                    ["timestamp"] = post.created_utc,
                    ["title"] = post.link_title,
                    ["state"] = PostState.Pending.ToString(),
                });

                clearReports.ExecuteNonQuery(new Dictionary<string, object>()
                {
                    ["postId"] = post.id,
                });

                foreach (var report in post.Reports)
                {
                    insertReportType.ExecuteNonQuery(new Dictionary<string, object>()
                    {
                        ["id"] = report.reason,
                    });

                    insertReport.ExecuteNonQuery(new Dictionary<string, object>()
                    {
                        ["postId"] = post.id,
                        ["reportTypeId"] = report.reason,
                        ["count"] = report.count,
                    });
                }

                // if this fails, it's OK, we'll just pick it up again on our next pass
                transaction.Commit();
            }

            UpdateActivePost(post.id);
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
            postDb.NextResult();

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
                    reason = reportReader.GetField<string>("reportTypeId"),
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
    }
}
