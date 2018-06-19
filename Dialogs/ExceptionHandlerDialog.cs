using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

namespace OmnBotQnamaker.Dialogs {

    [Serializable]
    public class ExceptionHandlerDialog : IDialog<object>
    {
        private readonly IDialog<object> _dialog;
        private readonly bool _displayException;
        private readonly int _stackTraceLength;

        public ExceptionHandlerDialog(IDialog<object> dialog, bool displayException, int stackTraceLength = 500)
        {
            _dialog = dialog;
            _displayException = displayException;
            _stackTraceLength = stackTraceLength;
        }

        public async Task StartAsync(IDialogContext context)
        {
            try
            {
                context.Call(_dialog, ResumeAsync);
            }
            catch (Exception e)
            {
                //add logging here

                if (_displayException)
                {
                    await DisplayException(context, e).ConfigureAwait(false);
                }
            }
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as IMessageActivity;

            // TODO: Put logic for handling user message here

            context.Wait(MessageReceivedAsync);
        }

        private async Task ResumeAsync(IDialogContext context, IAwaitable<object> result)
        {
            try
            {
                context.Done(await result);
            }
            catch (Exception e)
            {
                //add logging here

                if (_displayException)
                    await DisplayException(context, e).ConfigureAwait(false);
            }
        }

        private async Task DisplayException(IDialogContext context, Exception e)
        {

            var stackTrace = e.StackTrace;
            if (stackTrace.Length > _stackTraceLength)
                stackTrace = stackTrace.Substring(0, _stackTraceLength) + "…";
            stackTrace = stackTrace.Replace(Environment.NewLine, "  \n");

            var message = e.Message.Replace(Environment.NewLine, "  \n");

            var exceptionStr = $"**{message}**  \n\n{stackTrace}";

            await context.PostAsync(exceptionStr).ConfigureAwait(false);
        }
    }
}