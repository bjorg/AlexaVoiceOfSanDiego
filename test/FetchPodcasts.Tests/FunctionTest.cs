using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;

using FetchPodcasts;
using System.Xml.Linq;

namespace FetchPodcasts.Tests {

    public class FunctionTest {
        // [Fact]
        // public void TestToUpperFunction() {

        //     // Invoke the lambda function and confirm the string was upper cased.
        //     var function = new Function();
        //     var context = new TestLambdaContext();
        //     var upperCase = function.FunctionHandler("hello world", context);

        //     Assert.Equal("HELLO WORLD", upperCase);
        // }

        [Fact]
        public void ReadRSSFeed() {

            // arrange
            var rss = XDocument.Load("../../../rss.xml");

            // act
            var function = new Function();
            var podcasts = function.FindPodcasts(rss, 5).ToArray();

            // assert
            Assert.Equal(5, podcasts.Length);
            Assert.Equal("The Soccer, Homeless, Convention and Roads Ballot", podcasts[0].Title);
            Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170324_FULL_mixdown.mp3?dest-id=19280", podcasts[0].Url);
            Assert.Equal("Your Vacation Rental Is Now Illegal", podcasts[1].Title);
            Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170317_FULL_mixdown.mp3?dest-id=19280", podcasts[1].Url);
            Assert.Equal("The Other Thing SANDAG Hid", podcasts[2].Title);
            Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170310_FULL_mixdown.mp3?dest-id=19280", podcasts[2].Url);
            Assert.Equal("Mortgage Rates Are Low and Murder Rates Are Getting Higher", podcasts[3].Title);
            Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170303_FULL_mixdown.mp3?dest-id=19280", podcasts[3].Url);
            Assert.Equal("The Three Challenges to SoccerCity and the District's Budget Mess", podcasts[4].Title);
            Assert.Equal("http://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170224_FULL_mixdown.mp3?dest-id=19280", podcasts[4].Url);
        }
    }
}
