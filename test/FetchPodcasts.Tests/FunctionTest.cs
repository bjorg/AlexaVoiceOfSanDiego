/*
 * MIT License
 *
 * Copyright (c) 2017-2020 Voice of San Diego
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using LambdaSharp.ConfigSource;
using Xunit;

namespace VoiceOfSanDiego.Alexa.FetchPodcastsFunction.Tests {

    public class FunctionTest {

        //--- Methods ---
        [Fact]
        public async Task FindPodcastsInRssFeed() {

            // arrange
            var function = new Function();
            await function.InitializeAsync(new LambdaSharp.LambdaConfig(new LambdaDictionarySource(new Dictionary<string, string> {
                ["PodcastsRssFeed"] = "http://podcast.voiceofsandiego.org/rss",
                ["PodcastsLimit"] = "5",
                ["AlexaContents"] = "DynamoDB-Table"
            })));
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
        public async Task FetchLivePodcastFeed() {

            // arrange
            var function = new Function();
            await function.InitializeAsync(new LambdaSharp.LambdaConfig(new LambdaDictionarySource(new Dictionary<string, string> {
                ["PodcastsRssFeed"] = "http://podcast.voiceofsandiego.org/rss",
                ["PodcastsLimit"] = "5",
                ["AlexaContents"] = "DynamoDB-Table"
            })));

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
