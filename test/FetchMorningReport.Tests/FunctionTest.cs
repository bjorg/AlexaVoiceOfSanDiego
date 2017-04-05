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
        public void FetchLiveMorningReportFeed() {

            // arrange
            var function = new Function("http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/", null);

            // act
            var rss = function.FetchMorningReportFeedAsync().Result;

            // assert
            Assert.Equal("rss", rss.Elements().First().Name);
        }

#if false
        // TODO (bjorg, 2017-04-05): move test into `MorningReportLogic.Test`

        [Fact]
        public void ConvertLiveMorningReportToSsml() {

            // arrange
            var function = new Function("http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/", null);
            var rss = function.FetchMorningReportFeedAsync().Result;
            var morningReport = function.FindMorningReport(rss);

            // act
            var ssml = morningReport.ConvertContentsToSsml();

            // assert
            Console.WriteLine(ssml);
        }
#endif

#if false
        // TODO (bjorg, 2017-04-05): move test into `MorningReportLogic.Test`

        [Fact]
        public void ConvertLiveMorningReportToText() {

            // arrange
            var function = new Function("http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/", null);
            var rss = function.FetchMorningReportFeedAsync().Result;
            var morningReport = function.FindMorningReport(rss);

            // act
            var ssml = morningReport.ConvertContentsToText();

            // assert
            Console.WriteLine(ssml);
        }
#endif
    }
}
