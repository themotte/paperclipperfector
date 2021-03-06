
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            client.DefaultRequestHeaders.Add("User-Agent", Uri.EscapeDataString("hosted:net.pavlovian.paperclipperfector:unversioned (by /u/ZorbaTHut)"));

            accessClient = new HttpClient();

            RefreshAccessToken().Wait();
        }

        private struct AccessTokenResponse
        {
            public string access_token;
        }
        private async Task RefreshAccessToken()
        {
            Dbg.Inf("Refreshing access token . . .");

            while (true)
            {
                try
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

                    var result = await accessClient.SendAsync(message);
                    var content = await result.Content.ReadAsStringAsync();

                    var response = JsonConvert.DeserializeObject<AccessTokenResponse>(content);
                    client.DefaultRequestHeaders.Remove("Authorization");
                    client.DefaultRequestHeaders.Add("Authorization", $"bearer {response.access_token}");

                    return;
                }
                catch (Exception ex)
                {
                    Dbg.Ex(ex);

                    // This is probably Reddit freaking out. Try again later . . .
                    Thread.Sleep(15);
                }
            }
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
            public string name;
            public string author;
            public int ups;
            public string body_html;
            public string body;
            public string permalink;
            public long created_utc;
            public string url;

            // I have no idea why it uses these inconsistently, but it does
            public string title;
            public string link_title;

            public string link_id;

            public string[][] user_reports;
            public string[][] user_reports_dismissed;
            public string[][] mod_reports;
            public string[][] mod_reports_dismissed;

            public bool spam;
            public bool removed;

            public async Task<string> Title()
            {
                return title ?? link_title ?? await RedditApi.Instance.GetPostTitle(link_id);
            }

            public class Report
            {
                public string reason;
                public int count;
            }

            public IEnumerable<Report> Reports
            {
                get
                {
                    var numeric_reports = Enumerable.Empty<string[]>();
                    var tagged_reports = Enumerable.Empty<string[]>();

                    if (user_reports != null)
                    {
                        numeric_reports = numeric_reports.Concat(user_reports);
                    }

                    if (user_reports_dismissed != null)
                    {
                        numeric_reports = numeric_reports.Concat(user_reports_dismissed);
                    }

                    if (mod_reports != null)
                    {
                        tagged_reports = tagged_reports.Concat(mod_reports);
                    }

                    if (mod_reports_dismissed != null)
                    {
                        tagged_reports = tagged_reports.Concat(mod_reports_dismissed);
                    }

                    var numeric_reports_computed = numeric_reports.Select(raw => new Report() { reason = raw[0] ?? "", count = int.Parse(raw[1]) });
                    var tagged_reports_computed = tagged_reports.Select(raw => new Report() { reason = $"{raw[1]}: {raw[0]}", count = 1 });

                    return numeric_reports_computed.Concat(tagged_reports_computed);
                }
            }

            public IEnumerable<Report> ReportsNew
            {
                get
                {
                    var numeric_reports = Enumerable.Empty<string[]>();
                    var tagged_reports = Enumerable.Empty<string[]>();

                    if (user_reports != null)
                    {
                        numeric_reports = numeric_reports.Concat(user_reports);
                    }

                    if (mod_reports != null)
                    {
                        tagged_reports = tagged_reports.Concat(mod_reports);
                    }

                    var numeric_reports_computed = numeric_reports.Select(raw => new Report() { reason = raw[0] ?? "", count = int.Parse(raw[1]) });
                    var tagged_reports_computed = tagged_reports.Select(raw => new Report() { reason = $"{raw[1]}: {raw[0]}", count = 1 });

                    return numeric_reports_computed.Concat(tagged_reports_computed);
                }
            }
        }
        public async Task<Post> Entry(string fullname)
        {
            return await Entries(new string[] { fullname }).FirstAsync();
        }
        public async IAsyncEnumerable<Post> Entries(IEnumerable<string> fullnames)
        {
            // We need to do this anyway in order to send the request, so I might as well do it here.
            // I guess if I felt like being super-clever I could do this only up to the level of a single request so we get cute deferred processing as we go.
            // But I am not feeling that clever and it really does not matter for me.
            var names = fullnames.ToArray();
            int results = 0;

            // Alright, magic numbers.
            // There's no official answer for how many you can do at once, but 200 didn't work, and 100 does. So I'm sticking with 100.
            // You do get a pretty hilarious variety of error results with more than 100, though!
            const int singleRequestLimit = 100;

            // Do the actual nested pass.
            for (int cursor = 0; cursor < names.Length; cursor += singleRequestLimit)
            {
                var nameChunk = names.Skip(cursor).Take(singleRequestLimit);
                await foreach (var post in StandardListing<Post>($"r/{Config.Instance.subreddit}/api/info", new Dictionary<string, string> { ["id"] = string.Join(",", nameChunk) }, false))
                {
                    ++results;
                    yield return post;
                }
            }

            if (results != names.Length)
            {
                Dbg.Err($"Got the wrong number of entries! Expected {names.Length}, got {results}");
            }
        }
        public IAsyncEnumerable<Post> Reports()
        {
            return StandardListing<Post>($"r/{Config.Instance.subreddit}/about/reports");
        }

        public class ModerationLog
        {
            public string action;
            public string target_fullname;
            public long created_utc;
        }
        public IAsyncEnumerable<ModerationLog> ModerationLogs()
        {
            return StandardListing<ModerationLog>($"r/{Config.Instance.subreddit}/about/log");
        }

        private class NullResponse
        {
        }
        public async Task Approve(Post post)
        {
            if (Config.Instance.read_only)
            {
                return;
            }

            await SendRequest<NullResponse>("api/approve", new Dictionary<string, string> { ["id"] = post.name }, method: HttpMethod.Post);
        }

        private class ListingRequest
        {
            public string after;
        }
        public async IAsyncEnumerable<T> StandardListing<T>(string url, Dictionary<string, string> parameters = null, bool allowMultipage = true)
        {
            string after = null;

            Dictionary<string, string> params_writeable;
            if (parameters == null)
            {
                params_writeable = new Dictionary<string, string>();
            }
            else
            {
                params_writeable = new Dictionary<string, string>(parameters);
            }

            while (true)
            {
                params_writeable["after"] = after;
                var result = await SendRequest<Item<Listing<T>>>(url, params_writeable);
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

                if (!allowMultipage)
                {
                    break;
                }
            }
        }

        private async Task<T> SendRequest<T>(string url, Dictionary<string, string> input, HttpMethod method = null)
        {
            while (true)
            {
                try
                {
                    return await SendRequest_Internal<T>(url, input, method);
                }
                catch (System.Threading.Tasks.TaskCanceledException e)
                {
                    Dbg.Ex(e);

                    Thread.Sleep(TimeSpan.FromSeconds(15));

                    // try again
                }
                catch (UnauthorizedException e)
                {
                    Dbg.Ex(e);

                    // We've probably just had our access token time out
                    await RefreshAccessToken();

                    // try again
                }
                catch (HttpStatusException e)
                {
                    Dbg.Ex(e);

                    Thread.Sleep(TimeSpan.FromSeconds(15));

                    // try again
                }
                catch (Exception e)
                {
                    // uhhh
                    Dbg.Ex(e);

                    Thread.Sleep(TimeSpan.FromSeconds(15));

                    // . . . try again?
                }
            }
        }

        private class HttpStatusException : Exception { }
        private class UnauthorizedException : Exception { }

        private DateTimeOffset lastRequest = DateTimeOffset.MinValue;
        private async Task<T> SendRequest_Internal<T>(string url, Dictionary<string, string> input, HttpMethod method = null)
        {
            if (method == null)
            {
                method = HttpMethod.Get;
            }

            if (input == null)
            {
                input = new Dictionary<string, string>();
            }
            else
            {
                // so we don't modify anything
                input = new Dictionary<string, string>(input);
            }

            input["raw_json"] = "1";

            string uri = $"https://oauth.reddit.com/{url}?" + string.Join("&", input.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));
            if (true)
            {
                Dbg.Inf($"{method} Request: {uri}");
            }
            var message = new HttpRequestMessage(method, uri);

            // Pause for rate limiting
            {
                var msDelay = (DateTimeOffset.Now - lastRequest).TotalSeconds;
                if (msDelay < 1.1f)
                {
                    // :D :D :D :D round it so it's more accurate :D :D :D :D
                    Thread.Sleep((int)Math.Round((1.1f - msDelay) * 1000));
                }

                // Really we should be watching the headers to see how throttled we are, but right now we're not doing that.

                // And yes there's creep and inaccuracy all over the place here, but I'm not trying to minmax requests per minute, so whatever.
                lastRequest = DateTimeOffset.Now;
            }

            var result = await client.SendAsync(message);

            if (result.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedException();
            }

            if (result.StatusCode != HttpStatusCode.OK)
            {
                // Just try again later I guess
                Dbg.Err($"{uri}: {result.StatusCode}");

                throw new HttpStatusException();
            }

            var content = await result.Content.ReadAsStringAsync();

            if (verbose)
            {
                Dbg.Inf($"  {result.StatusCode}: {content}");
            }

            return JsonConvert.DeserializeObject<T>(content);
        }

        // Post titles never change, so we can just cache 'em as we go
        private Dictionary<string, string> postTitleCache = new Dictionary<string, string>();
        public async Task<string> GetPostTitle(string id)
        {
            string postTitle = postTitleCache.TryGetValue(id);

            if (postTitle == null)
            {
                postTitle = await (await Entry(id)).Title();
                postTitleCache[id] = postTitle;
            }

            return postTitle;
        }
    }
}
