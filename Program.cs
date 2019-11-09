using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace paperclipperfector
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new Thread(new ThreadStart(RedditScraper)).Start();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        public static void RedditScraper()
        {
            var reddit = new Reddit.RedditAPI(appId: Config.Instance.appId, appSecret: Config.Instance.appSecret, accessToken: Config.Instance.accessToken, refreshToken: Config.Instance.refreshToken);
            Dbg.Inf($"Username: {reddit.Account.Me.Name}");
            Dbg.Inf($"Cake Day: {reddit.Account.Me.Created:D}");
        }
    }
}
