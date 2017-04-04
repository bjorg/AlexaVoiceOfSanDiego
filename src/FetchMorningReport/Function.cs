using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using HtmlAgilityPack;
using Newtonsoft.Json;
using VoiceOfSanDiego.Alexa.Common;
using VoiceOfSanDiego.Alexa.MorningReport;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VoiceOfSanDiego.Alexa.FetchMorningReport {
    public class Function {

        //--- Types ---
        private class UnableToLoadRssFeed : Exception { }

        //--- Class Fields ---
        private static HttpClient _httpClient = new HttpClient();

        //--- Fields ---
        private readonly string _morningReportFeedUrl;
        private readonly string _dynamoTable;
        private readonly AmazonDynamoDBClient _dynamoClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);

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

        //--- Constructors ---
        public Function() {

            // read mandatory lambda function settings; without these, nothing works!
            _morningReportFeedUrl = Environment.GetEnvironmentVariable("morning_report_feed_url");
            _dynamoTable = Environment.GetEnvironmentVariable("dynamo_table");
        }

        public Function(string podcastFeedUrl, string dynamoTable) {
            _morningReportFeedUrl = podcastFeedUrl;
            _dynamoTable = dynamoTable;
        }

        //--- Methods ---
        public async Task FunctionHandler(ILambdaContext context) {
            if(_morningReportFeedUrl == null) {
                throw new Exception("missing configuration value 'podcasts_feed_url'");
            }
            if(_dynamoTable == null) {
                throw new Exception("missing configuration value 'dynamo_table'");
            }

            // retrieve RSS feed and parse it
            LambdaLogger.Log($"fetching morning report feed from '{_morningReportFeedUrl}'");
            var rss = await FetchMorningReportFeedAsync();

            // find up to the first morning report entry
            LambdaLogger.Log("finding most recent morning report");
            var morningReport = FindMorningReport(rss);
            LambdaLogger.Log("found morning report entries");

            // store morning report
            await SaveMorningReportAsync(morningReport);
            LambdaLogger.Log("updated morning report");
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
                Date = date,
                Author  = author,
                Document = doc
            };
        }

        public async Task<bool> SaveMorningReportAsync(MorningReportInfo morningReport) {
            if(morningReport == null) {
                return false;
            }
            var response = await _dynamoClient.PutItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = "morningreport" },
                ["Value"] = new AttributeValue { S = JsonConvert.SerializeObject(morningReport) },
                ["When"] = new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
            });
            return true;
        }
    }
}
