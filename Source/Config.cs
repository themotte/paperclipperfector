
using Newtonsoft.Json;
using System.IO;

namespace PaperclipPerfector
{
    public class Config
    {
        public string appId;
        public string appSecret;

        public string refreshToken;

        public string subreddit;

        private static Config StoredInstance;
        public static Config Instance
        {
            get
            {
                if (StoredInstance == null)
                {
                    StoredInstance = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
                }

                return StoredInstance;
            }
        }
    }
}
