
namespace PaperclipPerfector
{
    public class RedditScraper
    {
        public void Main()
        {
            foreach (var post in RedditApi.Instance.Reports())
            {
                Dbg.Inf("there was a post ~~");
            }
        }
    }
}
