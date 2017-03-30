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

            Console.WriteLine(morningReport);

            // assert
            // Assert.Equal(5, podcasts.Length);
            // Assert.Equal("The Soccer, Homeless, Convention and Roads Ballot", podcasts[0].Title);
            // Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170324_FULL_mixdown.mp3?dest-id=19280", podcasts[0].Url);
            // Assert.Equal("Your Vacation Rental Is Now Illegal", podcasts[1].Title);
            // Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170317_FULL_mixdown.mp3?dest-id=19280", podcasts[1].Url);
            // Assert.Equal("The Other Thing SANDAG Hid", podcasts[2].Title);
            // Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170310_FULL_mixdown.mp3?dest-id=19280", podcasts[2].Url);
            // Assert.Equal("Mortgage Rates Are Low and Murder Rates Are Getting Higher", podcasts[3].Title);
            // Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170303_FULL_mixdown.mp3?dest-id=19280", podcasts[3].Url);
            // Assert.Equal("The Three Challenges to SoccerCity and the District's Budget Mess", podcasts[4].Title);
            // Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170224_FULL_mixdown.mp3?dest-id=19280", podcasts[4].Url);
        }
    }
}
