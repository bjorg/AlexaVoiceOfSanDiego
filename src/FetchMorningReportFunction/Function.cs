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
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using HtmlAgilityPack;
using LambdaSharp;
using LambdaSharp.Schedule;
using VoiceOfSanDiego.Alexa.Common;
using VoiceOfSanDiego.Alexa.MorningReport;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VoiceOfSanDiego.Alexa.FetchMorningReportFunction {

    public class Function : ALambdaScheduleFunction {

        //--- Types ---
        private class UnableToLoadRssFeed : Exception { }

        //--- Class Fields ---
        private static HttpClient _httpClient = new HttpClient();

        //--- Class Methods ---
        public static XDocument ConvertHtmlToXml(string contents) {
            if(contents == null) {
                return null;
            }
            HtmlDocument html = new HtmlDocument();
            html.LoadHtml($"<html><body>{contents}</body></html>");
            html.OptionOutputAsXml = true;
            var xml = new StringWriter();
            html.Save(xml);
            return XDocument.Parse(xml.ToString());
        }

        //--- Fields ---
        private string _morningReportFeedUrl;
        private string _dynamoTable;
        private AmazonDynamoDBClient _dynamoClient;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _morningReportFeedUrl = config.ReadText("FetchMorningReportRssFeed");
            _dynamoTable = config.ReadDynamoDBTableName("AlexaContents");
            _dynamoClient = new AmazonDynamoDBClient();
        }

        public override async Task ProcessEventAsync(LambdaScheduleEvent schedule) {

            // retrieve RSS feed and parse it
            LogInfo($"fetching morning report feed from '{_morningReportFeedUrl}'");
            var rss = await FetchMorningReportFeedAsync();

            // find up to the first morning report entry
            LogInfo("finding most recent morning report");
            var morningReport = FindMorningReport(rss);
            LogInfo("found morning report entries");

            // store morning report
            await SaveMorningReportAsync(morningReport);
            LogInfo("updated morning report");
        }

        public async Task<XDocument> FetchMorningReportFeedAsync() {
            var response = await _httpClient.GetAsync(_morningReportFeedUrl);
            if(!response.IsSuccessStatusCode) {
                throw new UnableToLoadRssFeed();
            }
            return XDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        public MorningReportInfo FindMorningReport(XDocument rss) {
            var item = rss?.Element("rss")
                ?.Element("channel")
                ?.Element("item");
            if(item == null) {
                return null;
            }
            var title = item.Element("title")?.Value;
            var date = Utils.ParseDate(item.Element("pubDate")?.Value);
            var author = item.Element("{http://purl.org/dc/elements/1.1/}creator")?.Value;
            var contents = item.Element("{http://purl.org/rss/1.0/modules/content/}encoded")?.Value;
            var doc = ConvertHtmlToXml(contents);
            if(doc == null) {
                return null;
            }
            return new MorningReportInfo {
                Title = title,
                Date = date ?? DateTime.UtcNow.Date,
                Author  = author,
                Document = doc
            };
        }

        public async Task<bool> SaveMorningReportAsync(MorningReportInfo morningReport) {
            if(morningReport == null) {
                return false;
            }
            var response = await _dynamoClient.PutItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = MorningReportInfo.ROW_KEY },
                ["Value"] = new AttributeValue { S = morningReport.ToJson() },
                ["When"] = new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
            });
            return true;
        }
    }
}
