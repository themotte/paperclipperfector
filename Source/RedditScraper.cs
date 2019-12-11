
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PaperclipPerfector
{
    public class RedditScraper
    {
        public void Main()
        {
            var importantLogs = new HashSet<string> { "approvecomment", "approvelink", "removecomment", "removelink" };

            while (true)
            {
                Dbg.Inf("Handling active reports . . .");
                foreach (var post in RedditApi.Instance.Reports())
                {
                    Db.Instance.UpdatePostData(post);
                }

                Dbg.Inf("Handling moderation logs . . .");
                var targetTimestamp = GlobalProps.Instance.lastScraped.Value;
                if (targetTimestamp == DateTimeOffset.MinValue)
                {
                    // If we have no target timestamp, use a month ago, just 'cause we have to do *something*
                    targetTimestamp = DateTimeOffset.Now - TimeSpan.FromDays(30);
                }
                else
                {
                    // If we have one, deduct five minutes from it, because we want a little bit of overlap just in case
                    targetTimestamp = targetTimestamp - TimeSpan.FromMinutes(5);
                }

                var readLogs = RedditApi.Instance.ModerationLogs()
                    .TakeWhile(log => DateTimeOffset.FromUnixTimeSeconds(log.created_utc) > targetTimestamp)
                    .Where(log => importantLogs.Contains(log.action)).ToArray();

                foreach (var entry in RedditApi.Instance.Entries(readLogs.Select(log => log.target_fullname)))
                {
                    Db.Instance.UpdatePostData(entry);
                }

                if (readLogs.Length > 0)
                {
                    GlobalProps.Instance.lastScraped.Value = DateTimeOffset.FromUnixTimeSeconds(readLogs[0].created_utc);
                }

                Dbg.Inf("Done with pass!");
                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
        }
    }
}
