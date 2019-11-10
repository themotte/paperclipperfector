
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace PaperclipPerfector
{
    public class RedditScraper
    {
        public void Main()
        {
            var reddit = new Reddit.RedditAPI(appId: Config.Instance.appId, appSecret: Config.Instance.appSecret, accessToken: Config.Instance.accessToken, refreshToken: Config.Instance.refreshToken);
            Dbg.Inf($"Username: {reddit.Account.Me.Name}");
            Dbg.Inf($"Cake Day: {reddit.Account.Me.Created:D}");
        }
    }
}
