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
using VoiceOfSanDiego.Alexa.Common;
using VoiceOfSanDiego.Alexa.MorningReport;
using VoiceOfSanDiego.Alexa.Podcasts;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VoiceOfSanDiego.Alexa.HandleAlexaPrompts {
    public class Function {

        //--- Constants ---
        private const string PROMPT_WELCOME = "Welcome to Voice of San Diego!";
        private const string PROMPT_HELP = "If you would like to hear the morning report, say 'Alexa, ask Voice to read me the morning report'.'";
        private const string PROMPT_NOT_SUPPORTED = "Sorry, that command is not yet supported.";
        private const string PROMPT_NOT_UNDERSTOOD = "Sorry, I don't know what you mean.";
        private const string PROMPT_ERROR_MORNING_REPORT = "Sorry, there was an error reading the morning report. Please try again later.";
        private const string PROMPT_ERROR_PODCAST = "Sorry, there was an error playing the podcast. Please try again later.";
        private const string PROMPT_ERROR_WHAT_IS_NEW = "Sorry, there was an error playing the podcast. Please try again later.";
        private const string INTENT_READ_MORNING_REPORT = "ReadMorningReport";
        private const string INTENT_PLAY_PODCAST = "PlayPodcast";
        private const string INTENT_HELP_ME = "HelpMe";
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
            LambdaLogger.Log($"received skill request ({skill.Request.GetType().Name}): request-id={skill.Request.RequestId}; session-id={skill.Session.SessionId}");
            if(_dynamoTable == null) {
                throw new Exception("missing configuration value 'dynamo_table'");
            }

            // decode skill request
            try {
                switch(skill.Request) {
                case LaunchRequest launch:
                    return BuildSpeechResponse(prompt: PROMPT_WELCOME, reprompt: PROMPT_HELP);
                case IntentRequest intent:
                    LambdaLogger.Log($"intent: {intent.Intent.Name}");
                    switch(intent.Intent.Name) {
                    case INTENT_READ_MORNING_REPORT:
                        return await BuildMorningReportResponseAsync();
                    case INTENT_PLAY_PODCAST:
                        return await BuildPodcastResponseAsync(0);
                    case INTENT_HELP_ME:
                    case INTENT_WHAT_IS_NEW:
                        return BuildSpeechResponse(PROMPT_NOT_SUPPORTED, reprompt: PROMPT_HELP);
                    case BuiltInIntent.Stop:
                    case BuiltInIntent.Cancel:
                    case BuiltInIntent.Pause:

                        // nothing to do
                        return null;
                    case BuiltInIntent.Resume:
                        LambdaLogger.Log("WARNING: not implemented");
                        return BuildSpeechResponse(PROMPT_NOT_SUPPORTED, reprompt: PROMPT_HELP);
                    default:
                        LambdaLogger.Log("WARNING: intent not recognized");
                        return BuildSpeechResponse(PROMPT_NOT_UNDERSTOOD, reprompt: PROMPT_HELP);
                    }
                case SessionEndedRequest ended:
                    LambdaLogger.Log("session ended");
                    return null;
                default:
                    LambdaLogger.Log($"unrecognized skill request: {JsonConvert.SerializeObject(skill)}");
                    return BuildSpeechResponse(PROMPT_NOT_UNDERSTOOD, reprompt: PROMPT_HELP);
                }
            } finally {
                LambdaLogger.Log($"finished skill request: request-id={skill.Request.RequestId}; session-id={skill.Session.SessionId}");
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
                    Card = BuildCard(morningReport.ConvertContentsToText()),
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
                var list = JsonConvert.DeserializeObject<PodcastInfo[]>(value.S);
                var item = list[podcastIndex];
                var prompt = $"Playing podcast entitled: \"{item.Title}\"";
                var result = new SkillResponse {
                    Version = "1.0",
                    Response = new ResponseBody {
                        OutputSpeech = new PlainTextOutputSpeech {
                            Text = prompt
                        },
                        Card = BuildCard(prompt),
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
            PodcastInfo podcast = null;
            foreach(var row in rows) {
                try {
                    switch(row["Key"].S) {
                        case MorningReportInfo.ROW_KEY:
                            morningReport = MorningReportInfo.FromJson(row["Value"].S);
                            break;
                        case PodcastInfo.ROW_KEY:
                            podcast = JsonConvert.DeserializeObject<PodcastInfo[]>(row["Value"].S)[0];
                            break;
                        default:

                            // unexpected item; just ignore it
                            break;
                    }
                } catch(Exception e) {
                    LambdaLogger.Log($"ERROR: unable to parse item ({e})");
                }
            }
            if((morningReport == null) && (podcast == null)) {
                return BuildSpeechResponse(PROMPT_ERROR_WHAT_IS_NEW);
            }

            var news = $"The latest morning reported is entitled: \"{morningReport.Title}\" and was published on {morningReport.Date}.\n" +
                $"The latest podcast is entitled: \"{podcast.Title}\" and was published on {podcast.Date}";
            return BuildSpeechResponse(news);
        }

        private SkillResponse BuildSpeechResponse(string prompt, string reprompt = null, bool shouldEndSession = true) {
            return new SkillResponse {
                Version = "1.0",
                Response = new ResponseBody {
                    OutputSpeech = new PlainTextOutputSpeech {
                        Text = prompt
                    },
                    Card = BuildCard(prompt),
                    Reprompt = (reprompt != null) ? new Reprompt {
                        OutputSpeech = new PlainTextOutputSpeech {
                            Text = reprompt
                        }
                    } : null,
                    ShouldEndSession = shouldEndSession
                }
            };
        }

        private ICard BuildCard(string prompt) {
            return new SimpleCard {
                Title = "Voice of San Diego",
                Content = prompt
            };
        }
    }
}
