using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using OmnBotQnamaker.Dialogs.QnADialog;
using OmnBotQnamaker.Global;

namespace OmnBotQnamaker.Dialogs
{
    [Serializable]
    public class LuisTriggerDialog : LuisDialog<object>
    {
        public LuisTriggerDialog() : base(new LuisService(new LuisModelAttribute(
            ConfigurationManager.AppSettings["LuisAppId"],
            ConfigurationManager.AppSettings["LuisAPIKey"])))
        {
        }

        [LuisIntent("None")]
        public async Task NoneIntent(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult result)
        {
            var activity = await message as IMessageActivity;
            var translatedText = await Translation.TranslateToEnglish(activity.Text);
            activity.Text = translatedText;
            await context.Forward(new CustomQnADialog(), ResumeAfterQnADialog, activity, CancellationToken.None);
        }

        // Go to https://luis.ai and create a new intent, then train/publish your luis app.
        // Finally replace "Greeting" with the name of your newly created intent in the following handler
        [LuisIntent("greeting")]
        public async Task GreetingIntent(IDialogContext context, LuisResult result)
        {
            // Greeting dialog
            await context.PostAsync("Hello! Feel free to ask me a question.");
        }

        [LuisIntent("goodbye")]
        public async Task CancelIntent(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("Goodbye, hope I could be of some use.");
        }

        [LuisIntent("question")]
        public async Task QuestionIntent(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult result)
        {
            var activity = await message as IMessageActivity;
            var translatedText = await Translation.TranslateToEnglish(activity.Text);
            activity.Text = translatedText;
            await context.Forward(new CustomQnADialog(), ResumeAfterQnADialog, activity, CancellationToken.None);
            //await this.ShowLuisResult(context, result);
        }

        private async Task ResumeAfterQnADialog(IDialogContext context, IAwaitable<object> result)
        {
            var message = await result as IMessageActivity;
            if (message != null)
            {
                await MessageReceived(context, result as IAwaitable<IMessageActivity>);
            }
            context.Wait(MessageReceived);
        }


        private async Task ShowLuisResult(IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"You have reached {result.Intents[0].Intent}. You said: {result.Query}");
            context.Wait(MessageReceived);
        }
    }
}