using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;

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

        //--- Methods ---

        public string FunctionHandler(string input, ILambdaContext context) {
            return input?.ToUpper();
        }

        public IEnumerable<PodcastInfo> FindPodcasts(XDocument rss, int limit) {
            return rss.Element("rss")
                .Element("channel")
                .Elements("item")
                .Take(limit)
                .Select(item => new PodcastInfo {
                    Title = item.Element("title").Value,
                    Url = item.Element("enclosure").Attribute("url").Value
                }).ToArray();
        }
    }
}
