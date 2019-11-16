using System.Collections.Generic;
using System.Data.SQLite;

namespace PaperclipPerfector
{
    public class Db
    {
        private SQLiteConnection dbConnection;

        private SQLiteCommand insertPost;
        private SQLiteCommand insertReportType;
        private SQLiteCommand clearReports;
        private SQLiteCommand insertReport;

        private SQLiteCommand readPosts;
        private SQLiteCommand readReportsFor;

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

        public Db()
        {
            // Init DB
            dbConnection = new SQLiteConnection("Data Source=db.sqlite");
            dbConnection.Open();

            // Init rows
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS posts (id TEXT PRIMARY KEY, author TEXT, html TEXT, ups INTEGER, permalink TEXT, timestamp INTEGER)");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS reportTypes (id TEXT PRIMARY KEY, assigned INTEGER, value INTEGER)");
            dbConnection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS reports (postId TEXT, reportTypeId TEXT, count INTEGER, PRIMARY KEY(postId, reportTypeId))");

            // Init commands
            insertPost = new SQLiteCommand("INSERT INTO posts(id, author, html, ups, permalink, timestamp) VALUES(@id, @author, @html, @ups, @permalink, @timestamp) ON CONFLICT(id) DO UPDATE SET html=excluded.html, ups=excluded.ups", dbConnection);
            insertReportType = new SQLiteCommand("INSERT OR IGNORE INTO reportTypes(id, assigned, value) VALUES(@id, 0, 0)", dbConnection);
            clearReports = new SQLiteCommand("DELETE FROM reports WHERE postId = @postId", dbConnection);
            insertReport = new SQLiteCommand("INSERT INTO reports(postId, reportTypeId, count) VALUES(@postId, @reportTypeId, @count)", dbConnection);

            readPosts = new SQLiteCommand("SELECT id, author, html, ups, permalink, timestamp FROM posts", dbConnection);
            readReportsFor = new SQLiteCommand("SELECT reportTypeId, count FROM reports WHERE postId = @postId", dbConnection);
        }

        public void UpdatePostData(RedditApi.Post post)
        {
            using (var transaction = dbConnection.BeginTransaction())
            {
                Dbg.Inf($"{post.author}");

                insertPost.ExecuteNonQuery(new Dictionary<string, object>()
                {
                    ["id"] = post.id,
                    ["author"] = post.author,
                    ["html"] = post.body_html,
                    ["ups"] = post.ups,
                    ["permalink"] = post.link_permalink,
                    ["timestamp"] = post.created_utc,
                });

                clearReports.ExecuteNonQuery(new Dictionary<string, object>()
                {
                    ["postId"] = post.id,
                });

                foreach (var report in post.Reports)
                {
                    Dbg.Inf($"    {report.reason}: {report.count}");

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
        }

        public RedditApi.Post[] ReadAllPosts()
        {
            // This is just for the sake of getting an atomic transaction
            using (var transaction = dbConnection.BeginTransaction())
            {
                var result = new List<RedditApi.Post>();

                var posts = readPosts.ExecuteReader();
                while (posts.Read())
                {
                    var post = new RedditApi.Post();
                    
                    post.id = posts.GetField<string>("id");
                    post.author = posts.GetField<string>("author");
                    post.body_html = posts.GetField<string>("html");
                    post.ups = (int)posts.GetField<long>("ups");    // this is janky but sqlite doesn't like casting to int, I guess
                    post.link_permalink = posts.GetField<string>("permalink");
                    post.created_utc = posts.GetField<long>("timestamp");

                    result.Add(post);
                }

                transaction.Rollback();

                return result.ToArray();
            }
        }
    }
}
