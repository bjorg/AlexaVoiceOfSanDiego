using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace VoiceOfSanDiego.Alexa.FetchMorningReport.Tests {
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
            Assert.NotNull(morningReport.Title);
            Assert.NotNull(morningReport.Date);
            Assert.NotNull(morningReport.Author);
            Assert.NotNull(morningReport.Document);
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
    }
}
