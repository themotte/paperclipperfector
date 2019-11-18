
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace PaperclipPerfector
{
    public class RedditApi
    {
        private static RedditApi StoredInstance;
        public static RedditApi Instance
        {
            get
            {
                if (StoredInstance == null)
                {
                    StoredInstance = new RedditApi();
                }

                return StoredInstance;
            }
        }

        private HttpClient client;
        private HttpClient accessClient;

        private bool verbose = false;

        private RedditApi()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Paperclip Perfector");

            accessClient = new HttpClient();

            RefreshAccessToken();
        }

        private struct AccessTokenResponse
        {
            public string access_token;
        }
        private void RefreshAccessToken()
        {
            var message = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token");
            message.Headers.Add("Authorization", "Basic " + System.Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Config.Instance.appId}:{Config.Instance.appSecret}")));

            var contentDict = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = Config.Instance.refreshToken,
            };

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", Config.Instance.refreshToken),
            });

            message.Content = formData;

            var result = accessClient.SendAsync(message).Result;
            var content = result.Content.ReadAsStringAsync().Result;

            var response = JsonConvert.DeserializeObject<AccessTokenResponse>(content);
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("Authorization", $"bearer {response.access_token}");
        }

        private class Item<T>
        {
            public T data;
        }

        private class Listing<T>
        {
            public Item<T>[] children;
            public string after;
        }

        // These structures are mostly intended for Reddit API communication.
        // Once they're put in the DB, they're not retrieved.
        public class Post
        {
            public string id;
            public string author;
            public int ups;
            public string body_html;
            public string permalink;
            public long created_utc;
            public string link_title;

            public string[][] user_reports;
            public string[][] mod_reports;

            public class Report
            {
                public string reason;
                public int count;
            }
            public IEnumerable<Report> Reports
            {
                get
                {
                    var all_reports = mod_reports.Concat(user_reports);

                    return all_reports.Select(raw => new Report() { reason = raw[0], count = int.Parse(raw[1]) });
                }
            }
        }

        public IEnumerable<Post> Reports()
        {
            return StandardListing<Post>("about/reports");
        }

        private class ListingRequest
        {
            public string after;
        }
        public IEnumerable<T> StandardListing<T>(string url)
        {
            string after = null;

            while (true)
            {
                var result = SendRequest<Item<Listing<T>>>(url, new Dictionary<string, string> { ["after"] = after });
                if (result.data == null)
                {
                    break;
                }

                foreach (var element in result.data.children)
                {
                    yield return element.data;
                }

                after = result.data.after;
                if (after == "" || after == null)
                {
                    break;
                }
            }
            
        }

        private T SendRequest<T>(string url, Dictionary<string, string> input)
        {
            if (input == null)
            {
                input = new Dictionary<string, string>();
            }

            input["raw_json"] = "1";

            string uri = $"https://oauth.reddit.com/r/{Config.Instance.subreddit}/{url}?" + string.Join("&", input.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));
            if (verbose)
            {
                Dbg.Inf($"Request: {uri}");
            }
            var message = new HttpRequestMessage(HttpMethod.Get, uri);

            //message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue();

            var result = client.SendAsync(message).Result;

            // check status code here, etc

            var content = result.Content.ReadAsStringAsync().Result;

            if (verbose)
            {
                Dbg.Inf($"  {result.StatusCode}: {content}");
            }

            return JsonConvert.DeserializeObject<T>(content);
        }
    }
}
