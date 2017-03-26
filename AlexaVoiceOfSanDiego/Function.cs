using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using Alexa.NET.Request;
using Alexa.NET.Response;
using Alexa.NET.Request.Type;
using System.Collections.Generic;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlexaVoiceOfSanDiego {
    public class Function {

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public SkillResponse FunctionHandler(SkillRequest skill, ILambdaContext context) {
            var requestType = skill.GetRequestType();

            switch(skill.Request) {
            case LaunchRequest launch:
                LambdaLogger.Log($"launch: {skill.Request.RequestId}");
                return BuildWelcomeResponse();
            case IntentRequest intent:
                LambdaLogger.Log($"intent: {skill.Request.RequestId}");
                return BuildWelcomeResponse();
            case SessionEndedRequest ended:
                LambdaLogger.Log($"ended: {skill.Request.RequestId}");
                return null;
            default:
                LambdaLogger.Log($"other: {skill.Request.RequestId}");
                return null;
            }
        }

        private SkillResponse BuildWelcomeResponse() {
            return BuildResponse("Welcome", "Welcome to VOSD", "Welcome again", shouldEndSession: false);
        }

        private SkillResponse BuildGoodByeResponse() {
            return BuildResponse("Good bye!", "Thank you!", null, shouldEndSession: true);
        }

        private SkillResponse BuildResponse(string title, string prompt, string reprompt, bool shouldEndSession, Dictionary<string, object> sessionAttributes = null) {
            return new SkillResponse {
                Version = "1.0",
                SessionAttributes = sessionAttributes,
                Response = new ResponseBody {
                    OutputSpeech = new PlainTextOutputSpeech {
                        Text = prompt
                    },
                    Card = new SimpleCard {
                        Title = title,
                        Content = prompt
                    },
                    Reprompt = new Reprompt {
                        OutputSpeech = new PlainTextOutputSpeech {
                            Text = reprompt
                        }
                    },
                    ShouldEndSession = shouldEndSession
                }
            };
        }
    }
}
