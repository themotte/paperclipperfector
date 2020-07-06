
using Newtonsoft.Json;
using System;
using System.IO;

namespace PaperclipPerfector
{
    public class Config
    {
        public string appId;
        public string appSecret;

        public string refreshToken;

        public string subreddit;

        public string password;

        public bool read_only = true;

        private static Config StoredInstance;
        public static Config Instance
        {
            get
            {
                if (StoredInstance == null)
                {
                    StoredInstance = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Path.Join(Datamount, "config.json")));
                }

                return StoredInstance;
            }
        }

        public static string Datamount
        {
            get
            {
                return Environment.GetEnvironmentVariable("PPDATAMOUNT") ?? ".";
            }
        }
    }
}
