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

namespace VoiceOfSanDiego.Alexa.FetchMorningReportFunction.Tests {
    public class FunctionTest {

        //--- Methods ---
        [Fact]
        public async Task FindMorningReportInRssFeed() {

            // arrange
            var function = new Function();
            await function.InitializeAsync(new LambdaSharp.LambdaConfig(new LambdaDictionarySource(new Dictionary<string, string> {
                ["FetchMorningReportRssFeed"] = "http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/",
                ["AlexaContents"] = "DynamoDB-Table"
            })));
            var rss = XDocument.Load("../../../rss.xml");

            // act
            var morningReport = function.FindMorningReport(rss);

            // assert
            Assert.NotNull(morningReport.Title);
            Assert.NotNull(morningReport.Author);
            Assert.NotNull(morningReport.Document);
        }

        [Fact]
        public async Task FetchLiveMorningReportFeed() {

            // arrange
            var function = new Function();
            await function.InitializeAsync(new LambdaSharp.LambdaConfig(new LambdaDictionarySource(new Dictionary<string, string> {
                ["FetchMorningReportRssFeed"] = "http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/",
                ["AlexaContents"] = "DynamoDB-Table"
            })));

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
            var function = new Function();
            await function.InitializeAsync(new LambdaSharp.LambdaConfig(new LambdaDictionarySource(new Dictionary<string, string> {
                ["FetchMorningReportRssFeed"] = "http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/",
                ["AlexaContents"] = "DynamoDB-Table"
            })));
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
            var function = new Function();
            await function.InitializeAsync(new LambdaSharp.LambdaConfig(new LambdaDictionarySource(new Dictionary<string, string> {
                ["FetchMorningReportRssFeed"] = "http://www.voiceofsandiego.org/category/newsletters/morning-report/feed/",
                ["AlexaContents"] = "DynamoDB-Table"
            })));
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
