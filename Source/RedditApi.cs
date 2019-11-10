
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

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

        private class ReportsResponse
        {

        }
        public IEnumerable<string> Reports()
        {
            SendRequest<ReportsResponse>("about/reports", null);

            yield break;
        }

        private T SendRequest<T>(string url, object request)
        {
            string requestSerialized = null;
            if (request is string)
            {
                requestSerialized = request as string;
            }
            else if (request != null)
            {
                requestSerialized = JsonConvert.SerializeObject(request);
            }

            string uri = $"https://oauth.reddit.com/r/{Config.Instance.subreddit}/{url}";
            var requestContent = request != null ? new StringContent(requestSerialized, Encoding.UTF8, "application/json") : null;
            var method = requestContent != null ? HttpMethod.Post : HttpMethod.Get;
            Dbg.Inf($"{method} {uri}: {requestSerialized}");
            var message = new HttpRequestMessage(requestContent != null ? HttpMethod.Post : HttpMethod.Get, uri);

            //message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue();

            var result = client.SendAsync(message).Result;

            // check status code here, etc

            var content = result.Content.ReadAsStringAsync().Result;

            Dbg.Inf($"  {result.StatusCode}: {content}");

            return JsonConvert.DeserializeObject<T>(content);
        }
    }
}
