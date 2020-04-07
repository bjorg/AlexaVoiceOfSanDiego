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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using Alexa.NET.Response.Directive;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using LambdaSharp;
using Newtonsoft.Json;
using VoiceOfSanDiego.Alexa.MorningReport;
using VoiceOfSanDiego.Alexa.Podcasts;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VoiceOfSanDiego.Alexa.HandleAlexaPromptsFunction {

    public class Function : ALambdaFunction<SkillRequest, SkillResponse> {

        //--- Constants ---
        private const string PROMPT_WELCOME = "Welcome to Voice of San Diego! ";
        private const string PROMPT_HELP_QUESTION = "Would you like to listen to what's new, the latest morning report, or the latest podcast? ";
        private const string PROMPT_GOOD_BYE = "Good bye! ";
        private const string PROMPT_NOT_SUPPORTED = "Sorry, that command is not yet supported. ";
        private const string PROMPT_NOT_UNDERSTOOD = "Sorry, I don't know what you mean. ";
        private const string PROMPT_ERROR_MORNING_REPORT = "Sorry, there was an error reading the morning report. Please try again later. ";
        private const string PROMPT_ERROR_PODCAST = "Sorry, there was an error playing the podcast. Please try again later. ";
        private const string PROMPT_ERROR_WHAT_IS_NEW = "Sorry, there was an error playing the podcast. Please try again later. ";
        private const string PROMPT_PAUSE = " ";
        private const string PROMPT_CANNOT_RESUME = "Sorry, I don't remember where to resume. ";
        private const string PROMPT_PODCAST_NOT_AVAILBLE = PROMPT_CANNOT_RESUME;
        private const string INTENT_READ_MORNING_REPORT = "ReadMorningReport";
        private const string INTENT_PLAY_PODCAST = "PlayPodcast";
        private const string INTENT_WHAT_IS_NEW = "WhatIsNew";

        //--- Types ---
        private class PodcastPlaybackInfo {

            //--- Fields ---
            public string UserId;
            public string Token;
            public string OffsetInMillisecondsText;

            //--- Properties ---
            [JsonIgnore]
            public int OffsetInMilliseconds {
                get {
                    int.TryParse(OffsetInMillisecondsText, out int value);
                    return value;
                }
            }
        }

        private class PlaybackFailedException : Exception {

            //--- Constructors ---
            public PlaybackFailedException(Error error) : base($"{error.Type}: {error.Message}") { }
        }

        private class RetrievePodcastsFailedException : Exception {

            //--- Constructors ---
            public RetrievePodcastsFailedException(HttpStatusCode httpStatusCode) : base($"unable to retrieve the podcasts row (status: {httpStatusCode})") { }
        }

        private class SystemExceptionRequestException : Exception {

            //--- Constructors ---
            public SystemExceptionRequestException(SystemExceptionRequest request) : base($"{request.Error.Type}: {request.Error.Message} ({JsonConvert.SerializeObject(request.ErrorCause)})") { }
        }

        //--- Class Methods ---
        private static string UserIdToResumeRecordKey(string userId) {
            var md5 = System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(userId));
            return $"resume-{new Guid(md5):N}";
        }

        //--- Fields ---
        private string _dynamoTable;
        private string _preHeadingBreak = "750ms";
        private string _postHeadingBreak = "250ms";
        private string _bulletBreak = "750ms";
        private AmazonDynamoDBClient _dynamoClient;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _dynamoTable = config.ReadDynamoDBTableName("AlexaContents");
            _preHeadingBreak = $"{config.ReadInt("PreHeadingPause")}ms";
            _postHeadingBreak = $"{config.ReadInt("PostHeadingPause")}ms";
            _bulletBreak = $"{config.ReadInt("BulletPause")}ms";
            _dynamoClient = new AmazonDynamoDBClient();
        }

        public override async Task<SkillResponse> ProcessMessageAsync(SkillRequest skill) {

            // decode skill request
            switch(skill.Request) {

            // skill was activated without an intent
            case LaunchRequest launch:
                LogInfo($"launch request");
                return BuildSpeechResponse(PROMPT_WELCOME + PROMPT_HELP_QUESTION, shouldEndSession: false);

            // skill was activated with an intent
            case IntentRequest intent:
                LogInfo($"intent request: {intent.Intent.Name}");
                switch(intent.Intent.Name) {

                // custom intents
                case INTENT_READ_MORNING_REPORT:
                    return await BuildMorningReportResponseAsync();
                case INTENT_PLAY_PODCAST:
                    return await BuildPodcastResponseAsync(0);
                case INTENT_WHAT_IS_NEW:
                    return await BuildWhatsNewResponseAsync();

                // built-in intents
                case BuiltInIntent.Help:
                    return BuildSpeechResponse(PROMPT_HELP_QUESTION, shouldEndSession: false);
                case BuiltInIntent.Stop:
                case BuiltInIntent.Cancel:
                    await DeletePodcastPlaybackAsync(skill.Context.System.User.UserId);
                    return BuildStopPodcastPlayback(PROMPT_GOOD_BYE);
                case BuiltInIntent.Pause:
                    return BuildStopPodcastPlayback(PROMPT_PAUSE);
                case BuiltInIntent.Resume:
                    var playback = await ReadPodcastPlaybackAsync(skill.Context.System.User.UserId);
                    return await BuildResumePodcastResponseAsync(playback);

                // unsupported built-in intents
                case BuiltInIntent.LoopOff:
                case BuiltInIntent.LoopOn:
                case BuiltInIntent.Next:
                case BuiltInIntent.Previous:
                case BuiltInIntent.Repeat:
                case BuiltInIntent.ShuffleOff:
                case BuiltInIntent.ShuffleOn:
                case BuiltInIntent.StartOver:
                    LogWarn($"not supported ({intent.Intent.Name})");
                    return BuildSpeechResponse(PROMPT_NOT_SUPPORTED);

                // unknown intent
                default:
                    LogWarn("intent not recognized");
                    return BuildSpeechResponse(PROMPT_NOT_UNDERSTOOD + PROMPT_HELP_QUESTION, shouldEndSession: false);
                }

            // skill audio-player status changed (no response expected)
            case AudioPlayerRequest audio:
                LogInfo($"audio request: {audio.AudioRequestType} ({JsonConvert.SerializeObject(audio)})");
                switch(audio.AudioRequestType) {
                    case AudioRequestType.PlaybackStarted:
                    case AudioRequestType.PlaybackFinished:
                        await DeletePodcastPlaybackAsync(skill.Context.System.User.UserId);
                        break;
                    case AudioRequestType.PlaybackStopped:
                        await WritePodcastPlaybackAsync(new PodcastPlaybackInfo {
                            UserId = skill.Context.System.User.UserId,
                            Token = audio.Token,
                            OffsetInMillisecondsText = audio.OffsetInMilliseconds.ToString()
                        });
                        break;
                    case AudioRequestType.PlaybackFailed:
                        LogError(new PlaybackFailedException(audio.Error));
                        break;
                    case AudioRequestType.PlaybackNearlyFinished:
                    default:
                        break;
                }
                return ResponseBuilder.Empty();

            // skill session ended (no response expected)
            case SessionEndedRequest ended:
                LogInfo("session ended");
                return ResponseBuilder.Empty();

            // exception reported on previous response (no response expected)
            case SystemExceptionRequest error:
                LogError(new SystemExceptionRequestException(error));
                return ResponseBuilder.Empty();

            // unknown skill received (no response expected)
            default:
                LogWarn($"unrecognized skill request: {JsonConvert.SerializeObject(skill)}");
                return ResponseBuilder.Empty();
            }
        }

        private async Task<SkillResponse> BuildMorningReportResponseAsync() {
            var response = await _dynamoClient.GetItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = MorningReportInfo.ROW_KEY }
            });
            AttributeValue value = null;
            if((response.HttpStatusCode != HttpStatusCode.OK) || !response.Item.TryGetValue("Value", out value)) {
                return BuildSpeechResponse(PROMPT_ERROR_MORNING_REPORT);
            }
            var morningReport = MorningReportInfo.FromJson(value.S);
            return new SkillResponse {
                Version = "1.0",
                Response = new ResponseBody {
                    OutputSpeech = new SsmlOutputSpeech {
                        Ssml = morningReport.ConvertContentsToSsml(_preHeadingBreak, _postHeadingBreak, _bulletBreak)
                    },
                    ShouldEndSession = true
                }
            };
        }

        private async Task<SkillResponse> BuildPodcastResponseAsync(int podcastIndex) {
            var list = await GetPodcastsAsync();
            if(podcastIndex >= list.Length) {
                return BuildSpeechResponse(PROMPT_PODCAST_NOT_AVAILBLE + PROMPT_HELP_QUESTION, shouldEndSession: false);
            }
            try {
                var item = list[podcastIndex];
                var prompt = $"Playing podcast entitled: \"{item.Title}\"";
                var result = new SkillResponse {
                    Version = "1.0",
                    Response = new ResponseBody {
                        OutputSpeech = new PlainTextOutputSpeech {
                            Text = prompt
                        },
                        ShouldEndSession = true
                    }
                };
                result.Response.Directives.Add(new AudioPlayerPlayDirective() {
                    PlayBehavior = PlayBehavior.ReplaceAll,
                    AudioItem = new AudioItem {
                        Stream = new AudioItemStream {
                            Url = item.Url,
                            Token = item.Token,
                            OffsetInMilliseconds = 0
                        }
                    }
                });
                return result;
            } catch(Exception e) {
                LogError(e, "unable to parse podcast #{0}", podcastIndex);
                return BuildSpeechResponse(PROMPT_ERROR_PODCAST);
            }
        }

        private async Task<SkillResponse> BuildResumePodcastResponseAsync(PodcastPlaybackInfo playback) {
            if(playback == null) {
                return BuildSpeechResponse(PROMPT_CANNOT_RESUME + PROMPT_HELP_QUESTION, shouldEndSession: false);
            }
            var list = await GetPodcastsAsync();
            var item = list.FirstOrDefault(p => p.Token == playback.Token);
            if(item == null) {
                return BuildSpeechResponse(PROMPT_PODCAST_NOT_AVAILBLE + PROMPT_HELP_QUESTION, shouldEndSession: false);
            }
            try {
                var prompt = $"Continue playing podcast entitled: \"{item.Title}\"";
                var result = new SkillResponse {
                    Version = "1.0",
                    Response = new ResponseBody {
                        OutputSpeech = new PlainTextOutputSpeech {
                            Text = prompt
                        },
                        ShouldEndSession = true
                    }
                };
                result.Response.Directives.Add(new AudioPlayerPlayDirective() {
                    PlayBehavior = PlayBehavior.ReplaceAll,
                    AudioItem = new AudioItem {
                        Stream = new AudioItemStream {
                            Url = item.Url,
                            Token = item.Token,
                            OffsetInMilliseconds = playback.OffsetInMilliseconds
                        }
                    }
                });
                return result;
            } catch(Exception e) {
                LogError(e, "unable to parse podcast (token='{0}', offset={1})", playback.Token, playback.OffsetInMillisecondsText);
                return BuildSpeechResponse(PROMPT_ERROR_PODCAST);
            }
        }

        private SkillResponse BuildStopPodcastPlayback(string prompt) {
            var result = new SkillResponse {
                Version = "1.0",
                Response = new ResponseBody {
                    OutputSpeech = new PlainTextOutputSpeech {
                        Text = prompt
                    },
                    ShouldEndSession = true
                }
            };
            result.Response.Directives.Add(new StopDirective());
            return result;
        }

        private async Task<SkillResponse> BuildWhatsNewResponseAsync() {
            var response = await _dynamoClient.BatchGetItemAsync(new Dictionary<string, KeysAndAttributes> {
                [_dynamoTable] = new KeysAndAttributes {
                    Keys = new List<Dictionary<string, AttributeValue>> {
                        new Dictionary<string, AttributeValue> {
                            ["Key"] = new AttributeValue { S = PodcastInfo.ROW_KEY }
                        },
                        new Dictionary<string, AttributeValue> {
                            ["Key"] = new AttributeValue { S = MorningReportInfo.ROW_KEY }
                        }
                    }
                }
            });
            List<Dictionary<string, AttributeValue>> rows;
            if((response.HttpStatusCode != HttpStatusCode.OK) || !response.Responses.TryGetValue(_dynamoTable, out rows)) {
                return BuildSpeechResponse(PROMPT_ERROR_WHAT_IS_NEW);
            }
            MorningReportInfo morningReport = null;
            PodcastInfo[] podcasts = null;
            foreach(var row in rows) {
                try {
                    switch(row["Key"].S) {
                        case MorningReportInfo.ROW_KEY:
                            morningReport = MorningReportInfo.FromJson(row["Value"].S);
                            break;
                        case PodcastInfo.ROW_KEY:
                            podcasts = PodcastInfo.FromJson(row["Value"].S);
                            break;
                        default:

                            // unexpected item; ignore it
                            break;
                    }
                } catch(Exception e) {

                    // log the exception and continue
                    LogError(e, "unable to parse item");
                }
            }
            if((morningReport == null) && (podcasts == null)) {
                return BuildSpeechResponse(PROMPT_ERROR_WHAT_IS_NEW);
            }
            var news = new StringBuilder();
            if(morningReport != null) {
                news.AppendLine($"The latest morning report is from {morningReport.Date.ToString("dddd, MMMM d")}, and is entitled: \"{morningReport.Title}\".");
            }
            if((podcasts != null) && (podcasts.Length > 0)) {
                news.AppendLine($"The latest podcast was recorded {podcasts[0].Date.ToString("dddd, MMMM d")}, and is entitled: \"{podcasts[0].Title}\".");
            }
            return BuildSpeechResponse(news.ToString() + PROMPT_HELP_QUESTION, shouldEndSession: false);
        }

        private SkillResponse BuildSpeechResponse(string prompt, string reprompt = null, bool shouldEndSession = true) {
            return new SkillResponse {
                Version = "1.0",
                Response = new ResponseBody {
                    OutputSpeech = new PlainTextOutputSpeech {
                        Text = prompt
                    },
                    Reprompt = (reprompt != null) ? new Reprompt {
                        OutputSpeech = new PlainTextOutputSpeech {
                            Text = reprompt
                        }
                    } : null,
                    ShouldEndSession = shouldEndSession
                }
            };
        }

        private async Task<PodcastInfo[]> GetPodcastsAsync() {
            var response = await _dynamoClient.GetItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = PodcastInfo.ROW_KEY }
            });
            AttributeValue value = null;
            if((response.HttpStatusCode != HttpStatusCode.OK) || !response.Item.TryGetValue("Value", out value)) {
                LogError(new RetrievePodcastsFailedException(response.HttpStatusCode));
                return null;
            }
            try {
                return PodcastInfo.FromJson(value.S);
            } catch(Exception e) {
                LogError(e, "unable to parse podcasts row");
                return null;
            }
        }

        private async Task WritePodcastPlaybackAsync(PodcastPlaybackInfo playback) {
            var response = await _dynamoClient.PutItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = UserIdToResumeRecordKey(playback.UserId) },
                ["Value"] = new AttributeValue { S = JsonConvert.SerializeObject(playback) },
                ["When"] = new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
            });
        }

        private async Task<PodcastPlaybackInfo> ReadPodcastPlaybackAsync(string userId) {
            var response = await _dynamoClient.GetItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = UserIdToResumeRecordKey(userId) }
            });
            AttributeValue value = null;
            if((response.HttpStatusCode != HttpStatusCode.OK) || !response.Item.TryGetValue("Value", out value)) {
                return null;
            }
            return JsonConvert.DeserializeObject<PodcastPlaybackInfo>(value.S);
        }

        private async Task DeletePodcastPlaybackAsync(string userId) {
            var response = await _dynamoClient.DeleteItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = UserIdToResumeRecordKey(userId) }
            });
        }
    }
}
