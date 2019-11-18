
using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperclipPerfector
{
    public class RedditScraper
    {
        public void Main()
        {
            var importantLogs = new HashSet<string> { "approvecomment", "approvelink", "removecomment", "removelink" };

            var mlogs = RedditApi.Instance.ModerationLogs()
                .TakeWhile(log => (DateTimeOffset.Now - DateTimeOffset.FromUnixTimeSeconds(log.created_utc)) < TimeSpan.FromDays(15))
                .Where(log => importantLogs.Contains(log.action));

            foreach (var entry in RedditApi.Instance.Entries(mlogs.Select(log => log.target_fullname)))
            {
                Db.Instance.UpdatePostData(entry);
            }

            Dbg.Inf("Done!");

            //RedditApi.Instance.Entry(RedditApi.Instance.ModerationLogs().ElementAt(2).target_fullname);

            /*foreach (var post in RedditApi.Instance.Reports())
            {
                Db.Instance.UpdatePostData(post);
            }*/
        }
    }
}
