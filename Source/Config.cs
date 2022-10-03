
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

        public string postgres;

        public string password;

        public bool read_only = true;

        private static Config StoredInstance;
        public static Config Instance
        {
            get
            {
                if (StoredInstance == null)
                {
                    StoredInstance = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Path.Join(Configmount, "config.json")));
                }

                return StoredInstance;
            }
        }

        private static string Configmount
        {
            get
            {
                return Environment.GetEnvironmentVariable("PPCONFIG") ?? ".";
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
