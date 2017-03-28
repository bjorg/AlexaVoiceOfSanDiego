using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace FetchPodcasts.Tests {

    public class FunctionTest {

        [Fact]
        public void FindFirst5PodcastsInRssFeed() {

            // arrange
            var rss = XDocument.Load("../../../rss.xml");

            // act
            var podcasts = Function.FindPodcasts(rss, 5);

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

        [Fact]
        public void FetchLivePodcastFeed() {

            // arrange

            // act
            var rss = Function.FetchPodcastFeed("http://podcast.voiceofsandiego.org/rss").Result;

            // assert
            Assert.Equal("rss", rss.Elements().First().Name);
        }
    }
}
