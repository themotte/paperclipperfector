
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

            new Thread(new RedditScraper().Main).Start();

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
