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

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace HandleAlexaPrompts {
    public class Function {

        //--- Fields ---
        private readonly string _dynamoTable;
        private readonly AmazonDynamoDBClient _dynamoClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);

        //--- Constructors ---
        public Function() {

            // read mandatory lambda function settings; without these, nothing works!
            _dynamoTable = System.Environment.GetEnvironmentVariable("dynamo_table");
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
                    return BuildSpeechResponse(
                        prompt: "Welcome to Voice of San Diego!",
                        reprompt: "If you would like to hear the morning report, say 'Alexa, ask Voice to read me the morning report'.'",
                        shouldEndSession: false
                    );
                case IntentRequest intent:
                    LambdaLogger.Log($"intent: {intent.Intent.Name}");
                    switch(intent.Intent.Name) {
                    case "ReadMorningReport":
                        return await BuildMorningReportResponse();
                    case "PlayPodcast":

                        // from: http://podcast.voiceofsandiego.org/rss
                        return BuildAudioResponse("https://traffic.libsyn.com/clean/vosd/VOSD_Podcast_20170324_FULL_mixdown.mp3", shouldEndSession: true);
                    case BuiltInIntent.Resume:
                    case BuiltInIntent.Pause:
                    case BuiltInIntent.Stop:
                    case BuiltInIntent.Cancel:
                        LambdaLogger.Log("WARNING: not implemented");
                        return BuildSpeechResponse("Sorry, that command is not yet supported.", reprompt: null, shouldEndSession: true);
                    default:
                        LambdaLogger.Log("WARNING: intent not recognized");
                        return BuildSpeechResponse("Sorry, I don't know what you mean.", reprompt: null, shouldEndSession: true);
                    }
                case SessionEndedRequest ended:
                    LambdaLogger.Log("session ended");
                    return null;
                default:
                    LambdaLogger.Log($"unrecognized skill request: {JsonConvert.SerializeObject(skill)}");
                    return null;
                }
            } finally {
                LambdaLogger.Log($"finished skill request: request-id={skill.Request.RequestId}; session-id={skill.Session.SessionId}");
            }
        }

        private async Task<SkillResponse> BuildMorningReportResponse() {
            var response = await _dynamoClient.GetItemAsync(_dynamoTable, new Dictionary<string, AttributeValue> {
                ["Key"] = new AttributeValue { S = "morningreport" }
            });
            AttributeValue value = null;
            if((response.HttpStatusCode != HttpStatusCode.OK) || !response.Item.TryGetValue("Value", out value)) {
                return BuildSpeechResponse("Sorry, there was an error reading the morning report. Please try again later.", reprompt: null, shouldEndSession: true);
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

        private SkillResponse BuildSpeechResponse(string prompt, string reprompt, bool shouldEndSession) {
            return new SkillResponse {
                Version = "1.0",
                Response = new ResponseBody {
                    OutputSpeech = new PlainTextOutputSpeech {
                        Text = prompt
                    },
                    Card = new SimpleCard {
                        Title = "Voice of San Diego",
                        Content = prompt
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

        private SkillResponse BuildAudioResponse(string url, bool shouldEndSession) {
            var result = new SkillResponse {
                Version = "1.0",
                Response = new ResponseBody {
                    ShouldEndSession = shouldEndSession
                }
            };
            result.Response.Directives.Add(new AudioPlayerPlayDirective() {
                PlayBehavior = PlayBehavior.ReplaceAll,
                AudioItem = new AudioItem() {
                    Stream = new AudioItemStream() {
                        Url = url,
                        Token = Guid.NewGuid().ToString()
                    }
                }
            });
            return result;
        }
    }
}
