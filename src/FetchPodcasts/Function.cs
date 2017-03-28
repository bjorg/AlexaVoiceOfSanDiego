using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace FetchPodcasts {

    public class Function {

        //--- Types ---
        public class PodcastInfo {

            //--- Properties ---
            public string Title { get; set; }
            public string Url { get; set; }
        }

        public class UnableToLoadRssFeed : Exception { }

        //--- Class Fields ---
        private static HttpClient _httpClient = new HttpClient();

        //--- Class Methods ---
        public static async Task<XDocument> FetchPodcastFeed(string url) {
            var response = await _httpClient.GetAsync(url);
            if(!response.IsSuccessStatusCode) {
                throw new UnableToLoadRssFeed();
            }
            return XDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        public static PodcastInfo[] FindPodcasts(XDocument rss, int limit) {
            return rss.Element("rss")
                .Element("channel")
                .Elements("item")
                .Take(limit)
                .Select(item => new PodcastInfo {
                    Title = item.Element("title").Value,
                    Url = item.Element("enclosure").Attribute("url").Value
                }).ToArray();
        }

        //--- Fields ---
        private readonly string _feedUrl;
        private readonly int _limit;

        //--- Constructors ---
        public Function() {
            _feedUrl = System.Environment.GetEnvironmentVariable("podcasts_feed_url");
            if(!int.TryParse(System.Environment.GetEnvironmentVariable("podcasts_entries_limit"), out _limit)) {
                _limit = 5;
            }
        }

        //--- Methods ---
        public async Task<string> FunctionHandler(ILambdaContext context) {
            LambdaLogger.Log($"fetching podcast feed from '{_feedUrl}'");
            var rss = await FetchPodcastFeed(_feedUrl);
            LambdaLogger.Log($"extracting up to {_limit} podcast entries");
            var podcasts = FindPodcasts(rss, _limit);
            LambdaLogger.Log($"found {podcasts.Length} podcast entries");
            return "success!";
        }
    }
}
