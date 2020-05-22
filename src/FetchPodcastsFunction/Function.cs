/*
 * MIT License
 *
 * Copyright (c) 2017-2020 Voice of San Diego
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
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LambdaSharp;
using LambdaSharp.Schedule;
using VoiceOfSanDiego.Alexa.Common;
using VoiceOfSanDiego.Alexa.Podcasts;

namespace VoiceOfSanDiego.Alexa.FetchPodcastsFunction {

    public class Function : ALambdaScheduleFunction {

        //--- Types ---
        private class UnableToLoadRssFeed : Exception { }

        //--- Fields ---
        private string _podcastFeedUrl;
        private int _podcastsLimit;
        private string _dynamoTable;
        private AmazonDynamoDBClient _dynamoClient;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _podcastFeedUrl = config.ReadText("PodcastsRssFeed");
            _podcastsLimit = config.ReadInt("PodcastsLimit");
            _dynamoTable = config.ReadDynamoDBTableName("AlexaContents");
            _dynamoClient = new AmazonDynamoDBClient();
        }

        public override async Task ProcessEventAsync(LambdaScheduleEvent schedule) {

            // retrieve RSS feed and parse it
            LogInfo($"fetching podcast feed from '{_podcastFeedUrl}'");
            var rss = await FetchPodcastFeedAsync();

            // find up to the desired number of podcast entries
            LogInfo($"extracting up to {_podcastsLimit} podcast entries");
            var podcasts = FindPodcasts(rss);
            LogInfo($"found {podcasts.Length} podcast entries");

            // store podcast playlist
            if(podcasts.Any()) {
                await SavePodcastsAsync(podcasts);
                LogInfo($"updated podcasts playlist");
            }
        }

        public async Task<XDocument> FetchPodcastFeedAsync() {
            var response = await HttpClient.GetAsync(_podcastFeedUrl);
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
