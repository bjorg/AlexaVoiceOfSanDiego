using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace FetchPodcasts.Tests {

    public class FunctionTest {

        //--- Methods ---
        [Fact]
        public void FindPodcastsInRssFeed() {

            // arrange
            var function = new Function(null, null, 5);
            var rss = XDocument.Load("../../../rss.xml");

            // act
            var podcasts = function.FindPodcasts(rss);

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
            var function = new Function("http://podcast.voiceofsandiego.org/rss", null, 5);

            // act
            var rss = function.FetchPodcastFeedAsync().Result;

            // assert
            Assert.Equal("rss", rss.Elements().First().Name);
        }

        [Fact]
        public void WritePodcastsToTable() {

            // arrange
            // var function = new Function(null, "VOSD-AlexaContents", 5);
            // var podcasts = new[] {
            //     new Function.PodcastInfo { Title = "Podcast #1", Url = "https://example.org/some/podcast1" },
            //     new Function.PodcastInfo { Title = "Podcast #2", Url = "https://example.org/some/podcast2" },
            //     new Function.PodcastInfo { Title = "Podcast #3", Url = "https://example.org/some/podcast3" },
            //     new Function.PodcastInfo { Title = "Podcast #4", Url = "https://example.org/some/podcast4" },
            //     new Function.PodcastInfo { Title = "Podcast #5", Url = "https://example.org/some/podcast5" }
            // };

            // act
            // var results = function.SavePodcasts(podcasts).Result;

            // assert
            // Assert.Equal("rss", rss.Elements().First().Name);
        }
    }
}
