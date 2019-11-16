
using System.Collections.Generic;

namespace PaperclipPerfector
{
    public class RedditScraper
    {
        public void Main()
        {
            foreach (var post in RedditApi.Instance.Reports())
            {
                Db.Instance.UpdatePostData(post);
            }
        }
    }
}
