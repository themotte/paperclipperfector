
namespace PaperclipPerfector
{
    public class RedditScraper
    {
        public void Main()
        {
            foreach (var post in RedditApi.Instance.Reports())
            {
                Dbg.Inf($"{post.author}");
                foreach (var report in post.Reports)
                {
                    Dbg.Inf($"    {report.reason}: {report.count}");
                }
            }
        }
    }
}
