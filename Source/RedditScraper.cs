
using System.Collections.Generic;
using System.Data.SQLite;
using System.Security.Cryptography;

namespace PaperclipPerfector
{
    public class RedditScraper
    {
        private SQLiteConnection dbConnection;

        private SQLiteCommand insertPost;
        private SQLiteCommand insertReportType;
        private SQLiteCommand clearReports;
        private SQLiteCommand insertReport;

        public void Main()
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

            foreach (var post in RedditApi.Instance.Reports())
            {
                UpdatePostData(post);
            }
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

                transaction.Commit();
            }
        }
    }
}
