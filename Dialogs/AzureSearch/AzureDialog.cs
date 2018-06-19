using OmnBotQnamaker.Dialogs.QnADialog.Models;
using Microsoft.Bot.Builder.CognitiveServices.QnAMaker.Resource;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Configuration;
using System.Data.SqlClient;

namespace OmnBotQnamaker.Dialogs.AzureSearch
{
    [Serializable]
    public class AzureDialog : IDialog<object>
    {

        private Results searchResults;
        private UserFeedback feedbackRecord;
        private SearchResult displayedResult;
        private int selectionTries = 0;
        private string searchKey;
        private string indexUrl;
        private string scoringProfile;

        public Task StartAsync(IDialogContext context)
        {
            searchKey = ConfigurationManager.AppSettings["AzureSearchKey"];
            indexUrl = ConfigurationManager.AppSettings["AzureSearchUrl"];
            scoringProfile = "&scoringProfile=" + ConfigurationManager.AppSettings["ScoringProfile"];
            context.Wait(MessageReceived);
            return Task.CompletedTask;
        }

        protected async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
        {
            //await context.PostAsync("QnaDialog message received");
            var message = await item;
            this.feedbackRecord = new UserFeedback() { userQuestion = message.Text , userId = message.From.Id };
            await this.HandleMessage(context, message.Text);
        }

        private async Task HandleMessage(IDialogContext context, string queryText)
        {
            var response = await GetSearchResponse(queryText);

            if (!response.Value.Any())
            {
                await NoMatchHandler(context, queryText);
            }
            else
            {
                if ((response.Value[0].Score - response.Value[1].Score > 0.2))
                {
                    displayedResult = response.Value[0];
                    await DefaultMatchHandler(context, queryText, response.Value[0]);
                }
                else // Maybe add some score checking so that only answers within a certain interval is returned
                {
                    var tempResult = new Results();
                    double prevScore = -1;
                    tempResult.Value.Add(response.Value[0]);
                    foreach (var res in response.Value)
                    {
                        if (prevScore != -1)
                        {
                            var chkAnswer = tempResult.Value.Where(r => r.Answer == res.Answer).FirstOrDefault();
                            if (chkAnswer == null)
                            {
                                var scoreDiff = prevScore - res.Score;
                                if (scoreDiff > 0.15)
                                {
                                    tempResult.Value.Add(res);
                                } else
                                {
                                    break;
                                }
                            }
                        }
                        prevScore = res.Score;

                    }
                    if (tempResult.Value.Count == 1)
                    {
                        displayedResult = tempResult.Value[0];
                        await DefaultMatchHandler(context, queryText, tempResult.Value[0]);
                    } else
                    {
                        await QnAFeedbackHandler(context, queryText, tempResult);
                    }
                }

            }
        }

        public async Task<Results> GetSearchResponse(string query)
        {
            string responseString;

            //Build the URI
            var search = "&search=";
            var uriBase = new Uri(indexUrl);
            var builder = new UriBuilder($"{uriBase}{search}/{query}{scoringProfile}");

            //Send the POST request
            using (WebClient client = new WebClient())
            {
                //Set the encoding to UTF8
                client.Encoding = System.Text.Encoding.UTF8;

                //Add the subscription key header
                client.Headers.Add("api-key", $"{searchKey}");
                //client.Headers.Add("Content-Type", "application/json");
                try
                {
                    responseString = client.DownloadString(builder.Uri);
                }
                catch (WebException err)
                {
                    throw new Exception(err.Message);
                }

                try
                {
                    var response = JsonConvert.DeserializeObject<Results>(responseString);
                    if (response.Value.Any())
                    {
                        foreach (var result in response.Value)
                        {
                            result.Answer = result.Answer.Replace("\\n", "\n");
                        }
                    }
                    return response;
                }
                catch
                {
                    throw new Exception("Unable to deserialize QnA Maker response string.");
                }
            }
        }

        //When more than one reponse
        private async Task QnAFeedbackHandler(IDialogContext context, string userQuery, Results results)
        {
            this.searchResults = results;
            var qnaList = searchResults.Value;
            await context.PostAsync(CreateHeroCard(context, qnaList));

            context.Wait(HandleQuestionResponse);

        }

        //Creates the herocard for responses with more than one answer
        protected IMessageActivity CreateHeroCard(IDialogContext context, List<SearchResult> options)
        {
            var reply = context.MakeMessage();

            reply.Attachments = new List<Attachment>();
            List<CardAction> cardButtons = new List<CardAction>();
            foreach (var ans in options)
            {
                CardAction cardAction = new CardAction()
                {
                    Type = "postBack",
                    Title = ans.Question,
                    Value = ans.Question
                };
                cardButtons.Add(cardAction);
            }

            CardAction none = new CardAction()
            {
                Type = "postBack",
                Title = Resource.noneOfTheAboveOption,
                Value = Resource.noneOfTheAboveOption
            };
            cardButtons.Add(none);

            HeroCard card = new HeroCard()
            {
                Title = "Did you mean?",
                Buttons = cardButtons
            };
            Attachment atch = card.ToAttachment();
            reply.Attachments.Add(atch);
            return reply;
        }

        //Handles the user response from question seleciton
        protected async Task HandleQuestionResponse(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var selection = await result as IMessageActivity;
            bool match = false;
            if (this.searchResults != null)
            {
                if (selection.Text.Equals(Resource.noneOfTheAboveOption))
                {
                    await context.PostAsync("Alright, maybe try rephrasing the question. Or if you prefer here's a link to the documentation http://omnia-docs.readthedocs.io/en/latest/.");
                    context.Done(false);
                }
                else
                {
                    foreach (var sResults in this.searchResults.Value)
                    {
                        if (sResults.Question.Equals(selection.Text, StringComparison.OrdinalIgnoreCase))
                        {
                            displayedResult = sResults;
                            await context.PostAsync(sResults.Answer);
                            match = true;

                            if (feedbackRecord != null)
                            {
                                feedbackRecord.kbQuestionId = sResults.Id;
                            }
                        }
                    }

                    if (match == false )
                    {
                        if (selectionTries++ < 2)
                        {
                            await context.PostAsync("Sorry, could not match match with options presented, please choose an option from the list.");
                            context.Wait(HandleQuestionResponse);
                        }
                        else
                        {
                            await context.PostAsync("Clearly not looking for any of the options, trying to run the new query instead.");
                            selectionTries = 0;
                            await MessageReceived(context, result);
                        }
                    }
                    else
                    {
                        await this.AnswerFeedbackMessageAsync(context, context.Activity.AsMessageActivity(), searchResults);
                    }
                }
            }
        }

        // Handles the reponse when only 1 answers was returned
        public virtual async Task DefaultMatchHandler(IDialogContext context, string originalQueryText, SearchResult result)
        {
            //var messageActivity = ProcessResultAndCreateMessageActivity(context, ref result);
            var messageActivity = context.MakeMessage();
            //var temp = result.Answer.Replace("\\n", "\n");
            messageActivity.Text = result.Answer;
            await context.PostAsync(messageActivity);
            await AnswerFeedbackMessageAsync(context, messageActivity, result);
        }

        // Handles the no match scenario
        public virtual async Task NoMatchHandler(IDialogContext context, string originalQueryText)
        {
            await context.PostAsync("Unfortunately no match could be found for your question.");
            var searchResMessage = await DocumentSearchResultReply(context, originalQueryText);
            await context.PostAsync(searchResMessage);
            context.Done(false);
            //throw new Exception("Sorry, I cannot find an answer to your question.");
        }

        // Presents the user with the feedback question.
        protected async Task AnswerFeedbackMessageAsync(IDialogContext context, IMessageActivity message, SearchResult result)
        {

            var reply = context.MakeMessage();
            reply.Attachments = new List<Attachment>();
            List<CardAction> cardButtons = new List<CardAction>();
            CardAction cardActionYes = new CardAction()
            {
                Type = "postBack",
                Title = "Yes",
                Value = "Yes"
            };
            CardAction cardActionNo = new CardAction()
            {
                Type = "postBack",
                Title = "No",
                Value = "No"
            };
            HeroCard card = new HeroCard()
            {
                Title = "Was this answer helpful?",
                Buttons = cardButtons
            };
            cardButtons.Add(cardActionYes);
            cardButtons.Add(cardActionNo);
            Attachment atch = card.ToAttachment();
            reply.Attachments.Add(atch);
            await context.PostAsync(reply);
            context.Wait(this.HandleHelpfulResponse);

        }

        protected async Task AnswerFeedbackMessageAsync(IDialogContext context, IMessageActivity message, Results result)
        {

            if (result.Value.Count() != 0)
            {
                if (message.Text.Equals(Resource.noneOfTheAboveOption))
                {
                    await context.PostAsync("Alright, maybe try rephrasing the question. Or if you prefer here's a link to the documentation http://omnia-docs.readthedocs.io/en/latest/.");
                    context.Done(false);
                }
                else
                {
                    var reply = context.MakeMessage();
                    reply.Attachments = new List<Attachment>();
                    List<CardAction> cardButtons = new List<CardAction>();
                    CardAction cardActionYes = new CardAction()
                    {
                        Type = "postBack",
                        Title = "Yes",
                        Value = "Yes"
                    };
                    CardAction cardActionNo = new CardAction()
                    {
                        Type = "postBack",
                        Title = "No",
                        Value = "No"
                    };
                    HeroCard card = new HeroCard()
                    {
                        Title = "Was this answer helpful?",
                        Buttons = cardButtons
                    };
                    cardButtons.Add(cardActionYes);
                    cardButtons.Add(cardActionNo);
                    Attachment atch = card.ToAttachment();
                    reply.Attachments.Add(atch);
                    await context.PostAsync(reply);
                    context.Wait(this.HandleHelpfulResponse);
                }
            }
            else
            {
                await context.PostAsync("No match could be found");
                context.Done(false);
            }
        }

        // Handles the user respons from the feedback question
        protected async Task HandleHelpfulResponse(IDialogContext context, IAwaitable<object> result)
        {
            var mes = await result as IMessageActivity;
            if (mes.Text.Equals("Yes"))
            {
                await context.PostAsync($"Great, thanks for your feedback.");
                await StoreFeedback(context);
                context.Done("Yes");
            }
            else if (mes.Text.Equals("No"))
            {
                await context.PostAsync("Alright, thanks for the feedback, maybe the documentation can be of more use: http://omnia-docs.readthedocs.io/en/latest/");
                context.Done("No");
            } else
            {
                context.Done(mes.Text);
            }
        }

        //Creates the necessary objects and sends the request to the service class to update the knowledgebase with the userQuestion
        protected async Task StoreFeedback(IDialogContext context)
        {
            if (this.feedbackRecord != null && displayedResult != null)
            {
                // some logic to add feedback to sql DB
                AddFeedbackToDb(feedbackRecord, displayedResult);
                //RunSearchIndexer();
            }

        }

        //Tveksam
        protected async Task<IMessageActivity> DocumentSearchResultReply(IDialogContext context, string userQuery)
        {
            var message = context.MakeMessage();


            var queryKeywords = await Global.TextAnalytics.GetKeyphrases(userQuery);

            string baseQueryString = "http://omnia-docs.readthedocs.io/en/latest/search.html?q=";
            string endQueryString = "&check_keywords=yes&area=default";
            string userInput = "";

            for (int i = 0; i < queryKeywords.documents[0].keyPhrases.Length; i++)
            {
                var keyPhraseSplit = queryKeywords.documents[0].keyPhrases[i].Split(' ');
                for (int j = 0; j < keyPhraseSplit.Length; j++)
                {
                    userInput += keyPhraseSplit[j];
                    if (j != (keyPhraseSplit.Length - 1))
                    {
                        userInput += "+";
                    }
                }

                if (i != (queryKeywords.documents[0].keyPhrases.Length - 1))
                {
                    userInput += "+";
                }
            }

            var searchResultUrl = baseQueryString + userInput + endQueryString;

            message.Text = "Here's a link searchresults in the Omnia Documentation: " + searchResultUrl;

            return message;
        }

        private static void AddFeedbackToDb(UserFeedback feedback, SearchResult res) 
        {
            try
            {
                string cnString = ConfigurationManager.AppSettings["SQLConnectionString"];

                string query = $@"
                        INSERT INTO [dbo].[questions]
                                (QnaId
                                ,Question)
                        VALUES
                                ('{res.QnaId}', '{feedback.userQuestion}') 
                ";

                using (var connection = new SqlConnection(cnString))
                {
                    connection.Open();

                    Submit_Tsql_NonQuery(connection, "3 - Inserts",
                       query);

                }
            }
            catch (SqlException e)
            {
                
            }
        }

        static void Submit_Tsql_NonQuery(
                        SqlConnection connection,
                        string tsqlPurpose,
                        string tsqlSourceCode,
                        string parameterName = null,
                        string parameterValue = null
            )
        {

            using (var command = new SqlCommand(tsqlSourceCode, connection))
            {
                if (parameterName != null)
                {
                    command.Parameters.AddWithValue(  // Or, use SqlParameter class.
                       parameterName,
                       parameterValue);
                }
                int rowsAffected = command.ExecuteNonQuery();
            }
        }

        // Borde inte behövas - indexet borde uppdateras när en ny fråga läggs till..
        static void RunSearchIndexer()
        {
            string responseString;
            string inxBaseUrl = @"https://qna-search-service.search.windows.net/indexers/quest-idxr/run?api-version=2016-09-01";
            //Build the URI
            var uriBase = new Uri(inxBaseUrl);
            var builder = new UriBuilder($"{uriBase}");

            //Gör nåt vettigt med denna
            var adminKey = ConfigurationManager.AppSettings["AzureAdminKey"];

            //Send the POST request
            using (WebClient client = new WebClient())
            {
                //Set the encoding to UTF8
                client.Encoding = System.Text.Encoding.UTF8;

                //Add the subscription key header
                client.Headers.Add("api-key", $"{adminKey}");
                //client.Headers.Add("Content-Type", "application/json");
                try
                {
                    responseString = client.DownloadString(builder.Uri);
                }
                catch (WebException err)
                {
                    throw new Exception(err.Message);
                }
            }
        }
    }

}