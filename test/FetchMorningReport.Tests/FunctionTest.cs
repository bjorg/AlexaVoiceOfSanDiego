using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;

using FetchMorningReport;
using HtmlAgilityPack;
using System.IO;

namespace FetchMorningReport.Tests {
    public class FunctionTest {

        //--- Methods ---
        [Fact]
        public void FindMorningReportInRssFeed() {

            // arrange
            var function = new Function(null, null);
            var rss = XDocument.Load("../../../rss.xml");

            // act
            var morningReport = function.FindMorningReport(rss);

            // assert
            Assert.NotNull(morningReport);
        }

        [Fact]
        public void FetchLivePodcastFeed() {

            // arrange
            var function = new Function("http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/", null);

            // act
            var rss = function.FetchMorningReportFeedAsync().Result;

            // assert
            Assert.Equal("rss", rss.Elements().First().Name);
        }

        [Fact]
        public void ConvertEncodedContentsToText() {

            // arrange
            var function = new Function(null, null);
            var contents = File.ReadAllText("../../../morning-report.txt");

            // act
            var text = function.ConvertContentsToText(contents);

            // assert
            // Console.WriteLine(text);
        }

        [Fact]
        public void ConvertEncodedContentsToSsml() {

            // arrange
            var function = new Function(null, null);
            var contents = File.ReadAllText("../../../morning-report.txt");

            // act
            var text = function.ConvertContentsToSsml(contents);

            // assert
            Console.WriteLine(text);
        }
    }
}
