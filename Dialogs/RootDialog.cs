using System;
using System.Threading;
using System.Threading.Tasks;
using OmnBotQnamaker.Dialogs.QnADialog;
using OmnBotQnamaker.Dialogs.AzureSearch;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using OmnBotQnamaker.Global;

namespace OmnBotQnamaker.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        private int selectedQuery;

        public Task StartAsync(IDialogContext context)
        {
            //context.PostAsync("Root start async");
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            var translatedMessage = await Translation.TranslateToEnglish(activity.Text);
            activity.Text = translatedMessage;
            // use azure search as backend
            await context.Forward(new AzureDialog(), ResumeAfterQnADialog, activity, CancellationToken.None);

            // use qnamaker som backend
            //await context.Forward(new CustomQnADialog(), ResumeAfterQnADialog, activity, CancellationToken.None);

            // Different backends options
            //if (activity.Text.StartsWith (".azure"))
            //{
            //    selectedQuery = 1;
            //    var temp = activity.Text.Remove(0, 7);
            //    var translatedText = await Translation.TranslateToEnglish(temp);
            //    activity.Text = translatedText;
            //    await context.Forward(new AzureDialog(), ResumeAfterQnADialog, activity, CancellationToken.None);
            //} else if (activity.Text.StartsWith(".qnaParsed"))
            //{
            //    selectedQuery = 2;
            //    var temp = activity.Text.Remove(0, 11);
            //    var translatedText = await Translation.TranslateToEnglish(temp);
            //    activity.Text = translatedText;
            //    await context.Forward(new CustomQnADialog(), ResumeAfterQnADialog, activity, CancellationToken.None);
            //} else if (activity.Text.StartsWith(".qnaMan"))
            //{
            //    selectedQuery = 3;
            //    var temp = activity.Text.Remove(0, 5);
            //    var translatedText = await Translation.TranslateToEnglish(temp);
            //    activity.Text = translatedText;
            //    await context.Forward(new QnATest(), ResumeAfterQnADialog, activity, CancellationToken.None);
            //} else
            //{
            //    await context.PostAsync("Pick what service - .azure (azure search index), .qnaParsed (qnaService - parsed from documents) or .qnaMan (qnaService - not parsed))");
            //}
        }

        private async Task ResumeAfterQnADialog(IDialogContext context, IAwaitable<object> response)
        {
            var message = await response as string;

            if (message == "Yes" || message == "No" || message == null)
            {
                context.Wait(this.MessageReceivedAsync);
            }
            else
            {
                var activity = context.MakeMessage();
                var translatedText = await Translation.TranslateToEnglish(message);
                activity.Text = translatedText;
                switch (selectedQuery)
                {
                    case 1:
                        await context.Forward(new AzureDialog(), ResumeAfterQnADialog, activity, CancellationToken.None);
                        break;
                    case 2:
                        await context.Forward(new CustomQnADialog(), ResumeAfterQnADialog, activity, CancellationToken.None);
                        break;
                    //case 3:
                    //    await context.Forward(new QnATest(), ResumeAfterQnADialog, activity, CancellationToken.None);
                    //    break;
                    default:
                        await context.Forward(new AzureDialog(), ResumeAfterQnADialog, activity, CancellationToken.None);
                        break;
                }

            }

        }

    }
}