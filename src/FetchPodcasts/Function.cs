/*
 * MIT License
 *
 * Copyright (c) 2017 Voice of San Diego
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

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
using VoiceOfSanDiego.Alexa.Common;
using VoiceOfSanDiego.Alexa.Podcasts;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VoiceOfSanDiego.Alexa.FetchPodcasts {

    public class Function {

        //--- Types ---
        private class UnableToLoadRssFeed : Exception { }

        //--- Class Fields ---
        private static HttpClient _httpClient = new HttpClient();

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
            var rss = await FetchPodcastFeedAsync();

            // find up to the desired number of podcast entries
            LambdaLogger.Log($"extracting up to {_podcastsLimit} podcast entries");
            var podcasts = FindPodcasts(rss);
            LambdaLogger.Log($"found {podcasts.Length} podcast entries");

            // store podcast playlist
            if(podcasts.Any()) {
                await SavePodcastsAsync(podcasts);
                LambdaLogger.Log($"updated podcasts playlist");
            }
        }

        public async Task<XDocument> FetchPodcastFeedAsync() {
            var response = await _httpClient.GetAsync(_podcastFeedUrl);
            if(!response.IsSuccessStatusCode) {
                throw new UnableToLoadRssFeed();
            }
            return XDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        public PodcastInfo[] FindPodcasts(XDocument rss) {
            return rss?.Element("rss")
                ?.Element("channel")
                ?.Elements("item")
                ?.Take(_podcastsLimit)
                ?.Select(item => {
                    return new PodcastInfo {
                        Title = item.Element("title").Value,
                        Date = Utils.ParseDate(item.Element("pubDate").Value) ?? DateTime.UtcNow.Date,
                        Url = item.Element("enclosure").Attribute("url").Value.Replace("http://", "https://"),
                        Token = item.Element("guid").Value
                    };
                })
                ?.ToArray() ?? new PodcastInfo[0];
        }

        public async Task<bool> SavePodcastsAsync(PodcastInfo[] podcasts) {
            var response = await _dynamoClient.PutItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = PodcastInfo.ROW_KEY },
                ["Value"] = new AttributeValue { S = PodcastInfo.ToJson(podcasts) },
                ["When"] = new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
            });
            return true;
        }
    }
}
