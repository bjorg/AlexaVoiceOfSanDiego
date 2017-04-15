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

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VoiceOfSanDiego.Alexa.HandleAlexaPrompts {
    public class Function {

        //--- Constants ---
        private const string PROMPT_WELCOME = "Welcome to Voice of San Diego! ";

        private const string PROMPT_HELP_QUESTION = "Would you like to listen to what's new, the latest morning report, or the latest podcast? ";
        private const string PROMPT_GOOD_BYE = "Thank you and good bye! ";
        private const string PROMPT_NOT_SUPPORTED = "Sorry, that command is not yet supported. ";
        private const string PROMPT_NOT_UNDERSTOOD = "Sorry, I don't know what you mean. ";
        private const string PROMPT_ERROR_MORNING_REPORT = "Sorry, there was an error reading the morning report. Please try again later. ";
        private const string PROMPT_ERROR_PODCAST = "Sorry, there was an error playing the podcast. Please try again later. ";
        private const string PROMPT_ERROR_WHAT_IS_NEW = "Sorry, there was an error playing the podcast. Please try again later. ";
        private const string INTENT_READ_MORNING_REPORT = "ReadMorningReport";
        private const string INTENT_PLAY_PODCAST = "PlayPodcast";
        private const string INTENT_WHAT_IS_NEW = "WhatIsNew";

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
                return BuildSpeechResponse(PROMPT_WELCOME + PROMPT_HELP_QUESTION, shouldEndSession: false);

            // skill was activated with an intent
            case IntentRequest intent:
                LambdaLogger.Log($"intent: {intent.Intent.Name}");
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

                    // nothing to do
                    return BuildSpeechResponse(PROMPT_GOOD_BYE);
                case BuiltInIntent.Pause:

                    // TODO (bjorg, 2017-04-15): need to store the current playback state
                    LambdaLogger.Log($"WARNING: not implemented ({intent.Intent.Name})");
                    return BuildStopPodcastPlayback(PROMPT_GOOD_BYE);

                case BuiltInIntent.Resume:

                    // TODO (bjorg, 2017-04-05): need to restore the current playback state
                    LambdaLogger.Log($"WARNING: not implemented ({intent.Intent.Name})");
                    return BuildSpeechResponse(PROMPT_NOT_SUPPORTED);
                case BuiltInIntent.LoopOff:
                case BuiltInIntent.LoopOn:
                case BuiltInIntent.Next:
                case BuiltInIntent.Previous:
                case BuiltInIntent.Repeat:
                case BuiltInIntent.ShuffleOff:
                case BuiltInIntent.ShuffleOn:
                case BuiltInIntent.StartOver:
                    LambdaLogger.Log($"WARNING: not supported ({intent.Intent.Name})");
                    return BuildSpeechResponse(PROMPT_NOT_SUPPORTED);

                // unknown intent
                default:
                    LambdaLogger.Log("WARNING: intent not recognized");
                    return BuildSpeechResponse(PROMPT_NOT_UNDERSTOOD + PROMPT_HELP_QUESTION, shouldEndSession: false);
                }

            // skill audio-player status changed
            case AudioPlayerRequest audio:
                LambdaLogger.Log($"audio: {audio.AudioRequestType}");
                return null;

            // skill session ended
            case SessionEndedRequest ended:
                LambdaLogger.Log("session ended");
                return null;

            // unknown skill received
            default:
                LambdaLogger.Log($"WARNING: unrecognized skill request: {JsonConvert.SerializeObject(skill)}");
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
            var response = await _dynamoClient.GetItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = PodcastInfo.ROW_KEY }
            });
            AttributeValue value = null;
            if((response.HttpStatusCode != HttpStatusCode.OK) || !response.Item.TryGetValue("Value", out value)) {
                return BuildSpeechResponse(PROMPT_ERROR_PODCAST);
            }
            try {
                var list = PodcastInfo.FromJson(value.S);
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
                    AudioItem = new AudioItem() {
                        Stream = new AudioItemStream() {
                            Url = item.Url,
                            Token = item.Token
                        }
                    }
                });
                return result;
            } catch(Exception e) {
                LambdaLogger.Log($"ERROR: unable to parse podcast #{podcastIndex} ({e})");
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
                    LambdaLogger.Log($"ERROR: unable to parse item ({e})");
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
    }
}
