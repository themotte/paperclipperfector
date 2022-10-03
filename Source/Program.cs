
using System;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace PaperclipPerfector
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Init
            var db = Db.Instance;
            db.UpdateSchema();

            Export.Run();

            // live free! live free forever!
            //new RedditScraper().Spawner().ContinueWith(t => Console.WriteLine(t.Exception), System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
            new MotteScraper().Spawner().ContinueWith(t => Console.WriteLine(t.Exception), System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
