using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using HtmlAgilityPack;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace FetchMorningReport {
    public class Function {

        //--- Types ---
        private class UnableToLoadRssFeed : Exception { }

        //--- Class Fields ---
        private static HttpClient _httpClient = new HttpClient();
        private static readonly Regex _htmlEntitiesRegEx = new Regex("&(?<value>#(x[a-f0-9]+|[0-9]+)|[a-z0-9]+);", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        //--- Fields ---
        private readonly string _morningReportFeedUrl;
        private readonly string _dynamoTable;
        private readonly AmazonDynamoDBClient _dynamoClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);

        //--- Class Methods ---
        private static string DecodeHtmlEntities(string text) {
            return _htmlEntitiesRegEx.Replace(text, m => {
                string v = m.Groups["value"].Value;
                if(v[0] == '#') {
                    if(char.ToLowerInvariant(v[1]) == 'x') {
                        string value = v.Substring(2);
                        return ((char)int.Parse(value, NumberStyles.HexNumber)).ToString();
                    } else {
                        string value = v.Substring(1);
                        return ((char)int.Parse(value)).ToString();
                    }
                } else {
                    switch(v) {
                    case "amp":
                        return "&";
                    case "apos":
                        return "'";
                    case "gt":
                        return ">";
                    case "lt":
                        return "<";
                    case "quot":
                        return "\"";
                    }
                    return v;
                }
            }, int.MaxValue);
        }

        //--- Constructors ---
        public Function() {

            // read mandatory lambda function settings; without these, nothing works!
            _morningReportFeedUrl = System.Environment.GetEnvironmentVariable("morning_report_feed_url");
            _dynamoTable = System.Environment.GetEnvironmentVariable("dynamo_table");
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
            await SaveMorningReportAsync(ConvertContentsToSsml(morningReport));
            LambdaLogger.Log("updated morning report");
        }

        public async Task<XDocument> FetchMorningReportFeedAsync() {
            var response = await _httpClient.GetAsync(_morningReportFeedUrl);
            if(!response.IsSuccessStatusCode) {
                throw new UnableToLoadRssFeed();
            }
            return XDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        public string FindMorningReport(XDocument rss) {
            return rss?.Element("rss")
                ?.Element("channel")
                ?.Element("item")
                ?.Element("{http://purl.org/rss/1.0/modules/content/}encoded")
                ?.Value;
        }

        public string ConvertContentsToText(string contents) {

            // convert HTML encoded contents to plain text
            HtmlDocument html = new HtmlDocument();
            html.LoadHtml($"<html><body>{contents}</body></html>");
            html.OptionOutputAsXml = true;
            var xml = new StringWriter();
            html.Save(xml);
            var doc = XDocument.Parse(xml.ToString());

            // extract all inner text nodes
            var text = new StringBuilder();
            Visit(doc.Root);
            return text.ToString();

            void VisitNodes(IEnumerable<XNode> nodes) {
                foreach(var node in nodes) {
                    Visit(node);
                }
            }
            void Visit(XNode node) {
                switch(node) {
                case XElement xelement:
                    var name = xelement.Name.ToString();
                    switch(name) {
                    case "p":
                        VisitNodes(xelement.Nodes());
                        text.AppendLine();
                        break;
                    case "h1":
                        text.AppendLine();
                        text.Append("= ");
                        VisitNodes(xelement.Nodes());
                        text.Append(" =");
                        text.AppendLine();
                        break;
                    case "h2":
                        text.AppendLine();
                        text.Append("== ");
                        VisitNodes(xelement.Nodes());
                        text.Append(" ==");
                        text.AppendLine();
                        break;
                    case "h3":
                        text.AppendLine();
                        text.Append("=== ");
                        VisitNodes(xelement.Nodes());
                        text.Append(" ===");
                        text.AppendLine();
                        break;
                    case "h4":
                        text.AppendLine();
                        text.Append("==== ");
                        VisitNodes(xelement.Nodes());
                        text.Append(" ====");
                        text.AppendLine();
                        break;
                    case "h5":
                        text.AppendLine();
                        text.Append("===== ");
                        VisitNodes(xelement.Nodes());
                        text.Append(" =====");
                        text.AppendLine();
                        break;
                    case "h6":
                        text.AppendLine();
                        text.Append("====== ");
                        VisitNodes(xelement.Nodes());
                        text.Append(" ======");
                        text.AppendLine();
                        break;
                    default:
                        VisitNodes(xelement.Nodes());
                        break;
                    }
                    break;
                case XText xtext:
                    text.Append(DecodeHtmlEntities(xtext.Value));
                    break;
                }
            }
        }

        public string ConvertContentsToSsml(string contents) {

            // convert HTML encoded contents to plain text
            HtmlDocument html = new HtmlDocument();
            html.LoadHtml($"<html><body>{contents}</body></html>");
            html.OptionOutputAsXml = true;
            var xml = new StringWriter();
            html.Save(xml);
            var doc = XDocument.Parse(xml.ToString());

            // extract all inner text nodes
            var ssml = new XDocument(new XElement("speak"));
            Visit(ssml.Root, doc.Root);
            return ssml.ToString();

            void VisitNodes(XElement parent, IEnumerable<XNode> nodes) {
                foreach(var node in nodes) {
                    Visit(parent, node);
                }
            }
            void Visit(XElement parent, XNode node) {
                switch(node) {
                case XElement xelement:
                    var name = xelement.Name.ToString();
                    switch(name) {
                    case "p":
                        VisitNodes(parent, xelement.Nodes());
                        parent.Add(new XText(" "));
                        break;
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        var p = new XElement("p");
                        parent.Add(p);
                        VisitNodes(p, xelement.Nodes());
                        break;
                    default:
                        VisitNodes(parent, xelement.Nodes());
                        break;
                    }
                    break;
                case XText xtext:
                    parent.Add(new XText(DecodeHtmlEntities(xtext.Value)));
                    break;
                }
            }
        }

        public async Task<bool> SaveMorningReportAsync(string morningReport) {
            var response = await _dynamoClient.PutItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = "morningreport" },
                ["Value"] = new AttributeValue { S = morningReport },
                ["When"] = new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
            });
            return true;
        }
    }
}
