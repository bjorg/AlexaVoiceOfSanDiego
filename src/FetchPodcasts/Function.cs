using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Newtonsoft.Json;

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
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        //--- Fields ---
        private readonly string _podcastFeedUrl;
        private readonly int _podcastsLimit;
        private readonly string _dynamoTable;
        private readonly AmazonDynamoDBClient _dynamoClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);

        //--- Constructors ---
        public Function() {

            // read mandatory lambda function settings; without these, nothing works!
            _podcastFeedUrl = System.Environment.GetEnvironmentVariable("podcasts_feed_url");
            _dynamoTable = System.Environment.GetEnvironmentVariable("dynamo_table");

            // read optional lambda function settings
            if(!int.TryParse(System.Environment.GetEnvironmentVariable("podcasts_limit"), out _podcastsLimit)) {
                _podcastsLimit = 5;
            }
        }

        public Function(string podcastFeedUrl, string dynamoTable, int podcastsLimit) {
            _podcastFeedUrl = podcastFeedUrl;
            _dynamoTable = dynamoTable;
            _podcastsLimit = podcastsLimit;
        }

        //--- Methods ---
        public async Task FunctionHandler(ILambdaContext context) {
            if(_podcastFeedUrl == null) {
                throw new Exception("missing configuration value 'podcasts_feed_url'");
            }
            if(_dynamoTable == null) {
                throw new Exception("missing configuration value 'dynamo_table'");
            }

            // retrieve RSS feed and parse it
            LambdaLogger.Log($"fetching podcast feed from '{_podcastFeedUrl}'");
            var rss = await FetchPodcastFeed();

            // find up to the desired number of podcast entries
            LambdaLogger.Log($"extracting up to {_podcastsLimit} podcast entries");
            var podcasts = FindPodcasts(rss);
            LambdaLogger.Log($"found {podcasts.Length} podcast entries");

            // store podcast playlist
            if(podcasts.Any()) {
                await SavePodcasts(podcasts);
                LambdaLogger.Log($"updated podcasts playlist");
            }
        }

        public async Task<XDocument> FetchPodcastFeed() {
            var response = await _httpClient.GetAsync(_podcastFeedUrl);
            if(!response.IsSuccessStatusCode) {
                throw new UnableToLoadRssFeed();
            }
            return XDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        public PodcastInfo[] FindPodcasts(XDocument rss) {
            return rss.Element("rss")
                .Element("channel")
                .Elements("item")
                .Take(_podcastsLimit)
                .Select(item => new PodcastInfo {
                    Title = item.Element("title").Value,
                    Url = item.Element("enclosure").Attribute("url").Value
                }).ToArray();
        }

        public async Task<bool> SavePodcasts(PodcastInfo[] podcasts) {
            var response = await _dynamoClient.PutItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = "podcasts" },
                ["Value"] = new AttributeValue { S = JsonConvert.SerializeObject(podcasts) }
            });
            return true;
        }
    }
}
