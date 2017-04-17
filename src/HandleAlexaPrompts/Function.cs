using Amazon.Lambda.Core;
using Alexa.NET.Request;
using Alexa.NET.Response;
using Alexa.NET.Request.Type;
using Newtonsoft.Json;
using Alexa.NET.Response.Directive;
using System;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using VoiceOfSanDiego.Alexa.MorningReport;
using VoiceOfSanDiego.Alexa.Podcasts;
using System.Text;
using System.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VoiceOfSanDiego.Alexa.HandleAlexaPrompts {
    public class Function {

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

        //--- Class Methods ---
        private static string UserIdToResumeRecordKey(string userId) {
            var md5 = System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(userId));
            return $"resume-{new Guid(md5):N}";
        }

        //--- Fields ---
        private readonly string _dynamoTable;
        private readonly string _preHeadingBreak = "750ms";
        private readonly string _postHeadingBreak = "250ms";
        private readonly string _bulletBreak = "750ms";
        private readonly AmazonDynamoDBClient _dynamoClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);

        //--- Constructors ---
        public Function() {

            // read mandatory lambda function settings; without these, nothing works!
            _dynamoTable = Environment.GetEnvironmentVariable("dynamo_table");

            // read optional lambda function settings
            _preHeadingBreak = Environment.GetEnvironmentVariable("pre_heading_break") ?? _preHeadingBreak;
            _postHeadingBreak = Environment.GetEnvironmentVariable("post_heading_break") ?? _postHeadingBreak;
            _bulletBreak = Environment.GetEnvironmentVariable("bullet_break") ?? _bulletBreak;
        }

        public async Task<SkillResponse> FunctionHandler(SkillRequest skill, ILambdaContext context) {
            if(_dynamoTable == null) {
                throw new Exception("missing configuration value 'dynamo_table'");
            }

            // decode skill request
            switch(skill.Request) {

            // skill was activated without an intent
            case LaunchRequest launch:
                LambdaLogger.Log($"*** INFO: launch");
                return BuildSpeechResponse(PROMPT_WELCOME + PROMPT_HELP_QUESTION, shouldEndSession: false);

            // skill was activated with an intent
            case IntentRequest intent:
                LambdaLogger.Log($"*** INFO: intent: {intent.Intent.Name}");
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
                    LambdaLogger.Log($"*** WARNING: not supported ({intent.Intent.Name})");
                    return BuildSpeechResponse(PROMPT_NOT_SUPPORTED);

                // unknown intent
                default:
                    LambdaLogger.Log("*** WARNING: intent not recognized");
                    return BuildSpeechResponse(PROMPT_NOT_UNDERSTOOD + PROMPT_HELP_QUESTION, shouldEndSession: false);
                }

            // skill audio-player status changed (no response expected)
            case AudioPlayerRequest audio:
                LambdaLogger.Log($"*** INFO: audio: {audio.AudioRequestType}");
                LambdaLogger.Log($"*** DEBUG: audio request: {JsonConvert.SerializeObject(audio)}");
                switch(audio.AudioRequestType) {
                    case AudioRequestType.PlaybackStarted:
                    case AudioRequestType.PlaybackFinished:
                        await DeletePodcastPlaybackAsync(skill.Context.System.User.UserId);
                        break;
                    case AudioRequestType.PlaybackStopped:
                        await WritePodcastPlaybackAsync(new PodcastPlaybackInfo {
                            UserId = skill.Context.System.User.UserId,
                            Token = audio.Token,
                            OffsetInMillisecondsText = audio.OffsetInMilliseconds
                        });
                        break;
                    case AudioRequestType.PlaybackFailed:
                        LambdaLogger.Log($"*** ERROR: playback failed: {JsonConvert.SerializeObject(audio.Error)}");
                        break;
                    case AudioRequestType.PlaybackNearlyFinished:
                    default:
                        break;
                }

                // BUG (2017-04-16, bjorg): return empty json when possible (i.e. {})
                return null;

            // skill session ended (no response expected)
            case SessionEndedRequest ended:
                LambdaLogger.Log("*** INFO: session ended");

                // NOBUGTE (2017-04-16, bjorg): return empty json when possible (i.e. {})
                return null;

            // exception reported on previous response (no response expected)
            case SystemExceptionRequest error:

                // NOTE (2017-04-16, bjorg): there's currently no known way to avoid 'invalid response' exception,
                //                           so we just ignore them
                if(error.Error.Type == ErrorType.InvalidResponse) {
                    LambdaLogger.Log("*** INFO: invalid response (expected)");
                } else {
                    LambdaLogger.Log("*** INFO: system exception");
                    LambdaLogger.Log($"*** EXCEPTION: skill request: {JsonConvert.SerializeObject(skill)}");
                }

                // BUG (2017-04-16, bjorg): return empty json when possible (i.e. {})
                return null;

            // unknown skill received (no response expected)
            default:
                LambdaLogger.Log($"*** WARNING: unrecognized skill request: {JsonConvert.SerializeObject(skill)}");

                // BUG (2017-04-16, bjorg): return empty json when possible (i.e. {})
                return null;
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
                LambdaLogger.Log($"*** ERROR: unable to parse podcast #{podcastIndex} ({e})");
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
                LambdaLogger.Log($"*** ERROR: unable to parse podcast (token='{playback.Token}', offset={playback.OffsetInMillisecondsText}) ({e})");
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
                    LambdaLogger.Log($"*** ERROR: unable to parse item ({e})");
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
                LambdaLogger.Log($"*** ERROR: unable to retrieve the podcasts row (status: {response.HttpStatusCode})");
                return null;
            }
            try {
                return PodcastInfo.FromJson(value.S);
            } catch(Exception e) {
                LambdaLogger.Log($"*** ERROR: unable to parse podcasts row ({e})");
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
