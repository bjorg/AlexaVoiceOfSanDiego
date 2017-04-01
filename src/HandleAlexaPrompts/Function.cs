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
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace HandleAlexaPrompts {
    public class Function {

        //--- Constants ---
        private const string PROMPT_WELCOME = "Welcome to Voice of San Diego!";
        private const string PROMPT_HELP = "If you would like to hear the morning report, say 'Alexa, ask Voice to read me the morning report'.'";
        private const string PROMPT_NOT_SUPPORTED = "Sorry, that command is not yet supported.";
        private const string PROMPT_NOT_UNDERSTOOD = "Sorry, I don't know what you mean.";
        private const string PROMPT_ERROR_MORNING_REPORT = "Sorry, there was an error reading the morning report. Please try again later.";
        private const string PROMPT_ERROR_PODCAST = "Sorry, there was an error playing the podcast. Please try again later.";
        private const string INTENT_READ_MORNING_REPORT = "ReadMorningReport";
        private const string INTENT_PLAY_PODCAST = "PlayPodcast";
        private const string INTENT_HELP_ME = "HelpMe";
        private const string INTENT_WHAT_IS_NEW = "WhatIsNew";

        //--- Fields ---
        private readonly string _dynamoTable;
        private readonly string _smallLogoUrl;
        private readonly string _largeLogoUrl;
        private readonly AmazonDynamoDBClient _dynamoClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);

        //--- Constructors ---
        public Function() {

            // read mandatory lambda function settings; without these, nothing works!
            _dynamoTable = Environment.GetEnvironmentVariable("dynamo_table");
            _smallLogoUrl = Environment.GetEnvironmentVariable("small_logo_url");
            _largeLogoUrl = Environment.GetEnvironmentVariable("large_logo_url");
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

                        // nothing to do
                        return null;
                    case BuiltInIntent.Resume:
                    case BuiltInIntent.Pause:
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
                ["Key"] = new AttributeValue { S = "morningreport" }
            });
            AttributeValue value = null;
            if((response.HttpStatusCode != HttpStatusCode.OK) || !response.Item.TryGetValue("Value", out value)) {
                return BuildSpeechResponse(PROMPT_ERROR_MORNING_REPORT);
            }
            return new SkillResponse {
                Version = "1.0",
                Response = new ResponseBody {
                    OutputSpeech = new SsmlOutputSpeech {
                        Ssml = value.S
                    },
                    ShouldEndSession = true
                }
            };
        }

        private async Task<SkillResponse> BuildPodcastResponseAsync(int podcastIndex) {
            var response = await _dynamoClient.GetItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = "podcasts" }
            });
            AttributeValue value = null;
            if((response.HttpStatusCode != HttpStatusCode.OK) || !response.Item.TryGetValue("Value", out value)) {
                return BuildSpeechResponse(PROMPT_ERROR_PODCAST);
            }
            try {
                var json = JArray.Parse(value.S);
                var title = (string)json[podcastIndex]["Title"];
                var url = (string)json[podcastIndex]["Url"];
                var token = (string)json[podcastIndex]["Token"];
                var prompt = $"Playing podcast entitled: \"{title}\"";
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
                            Url = url,
                            Token = token
                        }
                    }
                });
                return result;
            } catch(Exception e) {
                LambdaLogger.Log($"ERROR: unable to parse podcst #{podcastIndex} ({e})");
                return BuildSpeechResponse(PROMPT_ERROR_PODCAST);
            }
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
