using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PaperclipPerfector
{
    public class MotteScraper
    {
        private Npgsql.NpgsqlConnection dbConnection;

        private CommandTemplatePostgres getPostReports;
        private CommandTemplatePostgres getCommentReports;

        private CommandTemplatePostgres getPostData;
        private CommandTemplatePostgres getCommentData;

        public MotteScraper()
        {
            // Init DB
            dbConnection = new Npgsql.NpgsqlConnection($"Host={Config.Instance.postgres};Username=postgres;Password=postgres");
            dbConnection.Open();

            // Init commands
            getPostReports = new CommandTemplatePostgres("SELECT post_id AS id, reason FROM flags", dbConnection);
            getCommentReports = new CommandTemplatePostgres("SELECT comment_id AS id, reason FROM commentflags", dbConnection);

            getPostData = new CommandTemplatePostgres("SELECT submissions.id AS id, username, body_html, upvotes - downvotes as score, submissions.created_utc AS created_utc, title, submissions.body AS body FROM submissions, users WHERE author_id = users.id AND submissions.id = ANY (@items)", dbConnection);
            getCommentData = new CommandTemplatePostgres("SELECT comments.id AS id, username, comments.body_html as body_html, comments.upvotes - comments.downvotes as score, comments.created_utc AS created_utc, submissions.title as title, comments.body AS body FROM comments, users, submissions WHERE comments.author_id = users.id AND parent_submission = submissions.id AND comments.id = ANY (@items)", dbConnection);
        }

        public async Task Spawner()
        {
            while (true)
            {
                try
                {
                    await Main();
                }
                catch (Exception e)
                {
                    Dbg.Inf("AN EXCEPTION");
                    Dbg.Ex(e);
                    Dbg.Inf("MORE AN EXCEPTION");

                    // abort and restart, thereby bouncing everything
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                    Dbg.Inf("IT DIDN'T WORK");

                    await Task.Delay(TimeSpan.FromHours(1));
                }
            }
        }
        
        public async Task Main()
        {
            while (true)
            {
                ProcessType(getPostReports, getPostData, "MP", "post");
                ProcessType(getCommentReports, getCommentData, "MC", "comment");

                Dbg.Inf("Done with pass!");
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        public void ProcessType(CommandTemplatePostgres getReports, CommandTemplatePostgres getData, string prefix, string urltag)
        {
            var reportReader = getReports.ExecuteReader();
            var reportData = new Dictionary<int, List<string>>();
            while (reportReader.Read())
            {
                int id = reportReader.GetField<int>("id");
                if (!reportData.ContainsKey(id))
                {
                    reportData.Add(id, new List<string>());
                }

                reportData[id].Add(reportReader.GetField<string>("reason"));
            }
            reportReader.Close();

            var itemReader = getData.ExecuteReader(new Dictionary<string, object>()
            {
                ["items"] = reportData.Keys.ToArray(),
            });
            while (itemReader.Read())
            {
                var Post = new MotteApi.Post();

                int id = itemReader.GetField<int>("id");

                Post.id = $"{prefix}{id}";
                Post.author = itemReader.GetField<string>("username");
                Post.html = itemReader.GetField<string>("body_html");
                Post.text = itemReader.GetField<string>("body");
                Post.score = itemReader.GetField<int>("score");
                Post.permalink = $"https://www.themotte.org/{urltag}/{id}";
                Post.timestamp = itemReader.GetField<long>("created_utc");
                Post.title = itemReader.GetField<string>("title");

                Post.reports = reportData[id].GroupBy(s => s).Select(g => new MotteApi.Post.Report() { reason = g.Key, count = g.Count() }).ToArray();

                Db.Instance.UpdatePostData(Post);
            }
            itemReader.Close();
        }
    }
}
