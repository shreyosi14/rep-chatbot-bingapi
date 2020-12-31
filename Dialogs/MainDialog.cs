// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json;
using System.Web;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly InterviewQuestionRecognizer _luisRecognizer;
        protected readonly ILogger Logger;

        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(InterviewQuestionRecognizer luisRecognizer, ILogger<MainDialog> logger)
            : base(nameof(MainDialog))
        {
            _luisRecognizer = luisRecognizer;
            Logger = logger;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("NOTE: LUIS is not configured. To enable all capabilities, add 'LuisAppId', 'LuisAPIKey' and 'LuisAPIHostName' to the appsettings.json file.", inputHint: InputHints.IgnoringInput), cancellationToken);

                return await stepContext.NextAsync(null, cancellationToken);
            }

            // Use the text provided in FinalStepAsync or the default if it is the first time.
            var messageText = stepContext.Options?.ToString() ?? "I am ready to anwer your interview questions, you can ask me questions like\n\"What is Azure Devops?\"";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                var messageText = stepContext.Options?.ToString() ?? "LUIS is not configured";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
                // LUIS is not configured, we just run the BookingDialog path with an empty BookingDetailsInstance.
               
            }

            // Call LUIS and gather any potential booking details. (Note the TurnContext has the response to the prompt.)
            var luisResult = await _luisRecognizer.RecognizeAsync<InterviewQuestion>(stepContext.Context, cancellationToken);
            switch (luisResult.TopIntent().intent)
            {
                case InterviewQuestion.Intent.Intro:
                    
                    var strGreet = "Hello!!";
                    return await stepContext.BeginDialogAsync(strGreet, strGreet, cancellationToken);

                case InterviewQuestion.Intent.Topics:
                    
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "00df56db5228458d89962de3bcd0a715");
                    var queryString = HttpUtility.ParseQueryString(string.Empty);
                    queryString["q"] = stepContext.Context.Activity.Text.ToString();
                    var query = "https://api.bing.microsoft.com/v7.0/search?" + queryString;

                    // Run the query
                    HttpResponseMessage httpResponseMessage = client.GetAsync(query).Result;

                    // Deserialize the response content
                    var responseContentString = httpResponseMessage.Content.ReadAsStringAsync().Result;
                    Newtonsoft.Json.Linq.JObject responseObjects = Newtonsoft.Json.Linq.JObject.Parse(responseContentString);
                    string[,] responseUrl = new string[4, 3];
                    string[] responseSnippet = new string[4];
                    string[] responseName = new string[4];
                    // Handle success and error codes
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        JObject joResponse = JObject.Parse(responseObjects.ToString());
                        JObject ojObject = (JObject)joResponse["webPages"];
                        JArray ObjArray = (JArray)ojObject["value"];
                        //string ObjUrl = ObjArray.First["url"].ToString();
                        for (int i = 0; i < 4; i++)
                        {
                            responseUrl[i, 0] = ObjArray[i]["url"].ToString();
                            responseUrl[i, 1] = ObjArray[i]["snippet"].ToString();
                            responseUrl[i, 2] = ObjArray[i]["name"].ToString();
                        }


                    }
                    else
                    {
                        Console.WriteLine($"HTTP error status code: {httpResponseMessage.StatusCode.ToString()}");
                    }

                    string newStr = string.Empty; ;
                    for (int i = 0; i < 4; i++)
                    {
                        newStr = newStr + responseUrl[i, 2] + responseUrl[i, 1] + responseUrl[i, 0] + "<br/>";
                    }

                   
                    var activity = MessageFactory.Carousel(
new Attachment[]
{
    new HeroCard(
        title: responseUrl[0, 2],
        subtitle:responseUrl[0, 1],
        //images: new CardImage[] { new CardImage(url: "imageUrl1.png") },
        buttons: new CardAction[]
        {
            new CardAction(title: "Read More", type: ActionTypes.OpenUrl, value: responseUrl[0, 0])
        })
    .ToAttachment(),
    new HeroCard(
        title: responseUrl[1, 2],
        subtitle:responseUrl[1, 1],
        //images: new CardImage[] { new CardImage(url: "imageUrl2.png") },
        buttons: new CardAction[]
        {
            new CardAction(title: "Read More", type: ActionTypes.OpenUrl, value: responseUrl[1, 0])
        })
    .ToAttachment(),
    new HeroCard(
        title: responseUrl[2, 2],
        subtitle:responseUrl[2, 1],
        //images: new CardImage[] { new CardImage(url: "imageUrl3.png") },
        buttons: new CardAction[]
        {
            new CardAction(title: "Read More", type: ActionTypes.OpenUrl, value: responseUrl[2, 0])
        })
    .ToAttachment(),
     new HeroCard(
        title: responseUrl[3, 2],
        subtitle:responseUrl[3, 1],
        //images: new CardImage[] { new CardImage(url: "imageUrl3.png") },
        buttons: new CardAction[]
        {
            new CardAction(title: "Read More", type: ActionTypes.OpenUrl, value: responseUrl[3, 0])
        })
    .ToAttachment()
});
                    await stepContext.Context.SendActivityAsync(activity);
                    //await stepContext.Context.SendActivityAsync(getWeatherMessage, cancellationToken);
                    break;

                default:
                    // Catch all for unhandled intents
                    var didntUnderstandMessageText = $"Sorry, I didn't get that. Please try asking in a different way (intent was {luisResult.TopIntent().intent})";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        // Shows a warning if the requested From or To cities are recognized as entities but they are not in the Airport entity list.
        // In some cases LUIS will recognize the From and To composite entities as a valid cities but the From and To Airport values
        // will be empty if those entity values can't be mapped to a canonical item in the Airport.
        

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            
            // Restart the main dialog with a different message the second time around
            var promptMessage = "I am ready to answer your next question.";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        }
    }
}
