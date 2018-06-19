// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Gary Pretty Github:
// https://github.com/GaryPretty
// 
// Code derived from existing dialogs within the Microsoft Bot Framework
// https://github.com/Microsoft/BotBuilder
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.

using OmnBotQnamaker.Dialogs.QnADialog.Models;
//using Microsoft.Bot.Builder.CognitiveServices.QnAMaker;
using Microsoft.Bot.Builder.CognitiveServices.QnAMaker.Resource;
using Microsoft.Bot.Builder.Dialogs;
//using Microsoft.Bot.Builder.Resource;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
//using QnAMakerDialog.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Configuration;

namespace OmnBotQnamaker.Dialogs.QnADialog
{
    [Serializable]
    public class CustomQnADialog : IDialog<object>
    {

        private QuestionResponse.QnAMakerResult qnaMakerResults;
        private UserFeedback feedbackRecord;
        private QnAService qnaService;
        private int selectionTries = 0;

        public Task StartAsync(IDialogContext context)
        {
            //context.PostAsync("StartAsync Qna");
            var isActiveFilter = new List<Metadata> { new Metadata { Name = "isactive", Value = "true" } };
            qnaService = new QnAService(
                    ConfigurationManager.AppSettings["QnAMakerBaseUri"],
                    ConfigurationManager.AppSettings["QnAMakerEndpointKey"],
                    ConfigurationManager.AppSettings["QnAMakerKnowledgebaseId"],
                    ConfigurationManager.AppSettings["QnAMakerSubscriptionKey"],
                    5,
                    isActiveFilter
                );
            //context.PostAsync("StartAsync Qna");
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
            var response = await qnaService.GetQnAMakerResponse(queryText);

            //if (HandlerByMaximumScore == null)
            //{
            //    HandlerByMaximumScore =
            //        new Dictionary<QnAMakerResponseHandlerAttribute, QnAMakerResponseHandler>(GetHandlersByMaximumScore());
            //}

            if (response.Answers.Any() && response.Answers.First().QnaId == -1 || response.Answers.Count() == 0 )
            {
                await NoMatchHandler(context, queryText);
            }
            else
            {
                if (response.Answers.Count() == 1)
                {
                    await DefaultMatchHandler(context, queryText, response);
                }
                else // Maybe add some score checking so that only answers within a certain interval is returned
                {
                    await QnAFeedbackHandler(context, queryText, response);
                }

            }
        }

        //When more than one reponse
        private async Task QnAFeedbackHandler(IDialogContext context, string userQuery, QuestionResponse.QnAMakerResult results)
        {
            this.qnaMakerResults = results;
            var qnaList = qnaMakerResults.Answers;
            await context.PostAsync(CreateHeroCard(context, qnaList));

            context.Wait(HandleQuestionResponse);

        }

        //Creates the herocard for responses with more than one answer
        protected IMessageActivity CreateHeroCard(IDialogContext context, QuestionResponse.QnaAnswer[] options)
        {
            var reply = context.MakeMessage();

            reply.Attachments = new List<Attachment>();
            List<CardAction> cardButtons = new List<CardAction>();
            foreach (var ans in options)
            {
                CardAction cardAction = new CardAction()
                {
                    Type = "postBack",
                    Title = ans.Questions[0],
                    Value = ans.Questions[0]
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
            if (this.qnaMakerResults != null)
            {
                if (selection.Text.Equals(Resource.noneOfTheAboveOption))
                {
                    await context.PostAsync("Alright, maybe try rephrasing the question. Or if you prefer here's a link to the documentation http://omnia-docs.readthedocs.io/en/latest/.");
                    context.Done(false);
                }
                else
                {
                    foreach (var qnaMakerResult in this.qnaMakerResults.Answers)
                    {
                        if (qnaMakerResult.Questions[0].Equals(selection.Text, StringComparison.OrdinalIgnoreCase))
                        {
                            await context.PostAsync(qnaMakerResult.Answer);
                            match = true;

                            if (feedbackRecord != null)
                            {
                                feedbackRecord.kbQuestionId = qnaMakerResult.QnaId;
                            }
                        }
                    }

                    if (match == false )
                    {
                        if (selectionTries++ < 2)
                        {
                            await context.PostAsync("Sorry, could not match response with options presented, please choose an option from the list.");
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
                        await this.AnswerFeedbackMessageAsync(context, context.Activity.AsMessageActivity(), qnaMakerResults);
                    }
                }
            }
        }

        // Handles the reponse when only 1 answers was returned
        public virtual async Task DefaultMatchHandler(IDialogContext context, string originalQueryText, QuestionResponse.QnAMakerResult result)
        {
            var messageActivity = ProcessResultAndCreateMessageActivity(context, ref result);
            messageActivity.Text = result.Answers.First().Answer;
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
        protected async Task AnswerFeedbackMessageAsync(IDialogContext context, IMessageActivity message, QuestionResponse.QnAMakerResult result)
        {

            if (result.Answers.Count() != 0)
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
            }
            else
            {
                context.Done(mes.Text);
            }
        }

        //Creates the necessary objects and sends the request to the service class to update the knowledgebase with the userQuestion
        protected async Task StoreFeedback(IDialogContext context)
        {
            if (this.feedbackRecord != null)
            {
                // Uncomment to check for more than 1 keyword before adding question to phrase
                //var kWords = await Global.TextAnalytics.GetKeyphrases(feedbackRecord.userQuestion);
                //if (kWords.documents[0].keyPhrases.Length >= 2) // if the query contains more than one keyword update the knowledge base with query
                //{
                    var kbUpdates = new KbUpdateRequest { update = new ItemsToUpdate { QnaList = new List<KbItemToUpdate>() } };
                    var questionToUpdate = new KbItemToUpdate() { qnaId = this.feedbackRecord.kbQuestionId, questions = new QuestionsUpdateModel { add = new string[] { this.feedbackRecord.userQuestion } } };
                    kbUpdates.update.QnaList.Add(questionToUpdate);
                    await qnaService.UpdateKnowledgebase(kbUpdates);
                //}
            }
        }

        protected static IMessageActivity ProcessResultAndCreateMessageActivity(IDialogContext context, ref QuestionResponse.QnAMakerResult result)
        {
            var message = context.MakeMessage();

            var attachmentsItemRegex = new Regex("((&lt;attachment){1}((?:\\s+)|(?:(contentType=&quot;[\\w\\/-]+&quot;))(?:\\s+)|(?:(contentUrl=&quot;[\\w:/.=?-]+&quot;))(?:\\s+)|(?:(name=&quot;[\\w\\s&?\\-.@%$!£\\(\\)]+&quot;))(?:\\s+)|(?:(thumbnailUrl=&quot;[\\w:/.=?-]+&quot;))(?:\\s+))+(/&gt;))", RegexOptions.IgnoreCase);
            var matches = attachmentsItemRegex.Matches(result.Answers.First().Answer);

            foreach (var attachmentMatch in matches)
            {
                result.Answers.First().Answer = result.Answers.First().Answer.Replace(attachmentMatch.ToString(), string.Empty);

                var match = attachmentsItemRegex.Match(attachmentMatch.ToString());
                string contentType = string.Empty;
                string name = string.Empty;
                string contentUrl = string.Empty;
                string thumbnailUrl = string.Empty;

                foreach (var group in match.Groups)
                {
                    if (group.ToString().ToLower().Contains("contenttype="))
                    {
                        contentType = group.ToString().ToLower().Replace(@"contenttype=&quot;", string.Empty).Replace("&quot;", string.Empty);
                    }
                    if (group.ToString().ToLower().Contains("contenturl="))
                    {
                        contentUrl = group.ToString().ToLower().Replace(@"contenturl=&quot;", string.Empty).Replace("&quot;", string.Empty);
                    }
                    if (group.ToString().ToLower().Contains("name="))
                    {
                        name = group.ToString().ToLower().Replace(@"name=&quot;", string.Empty).Replace("&quot;", string.Empty);
                    }
                    if (group.ToString().ToLower().Contains("thumbnailurl="))
                    {
                        thumbnailUrl = group.ToString().ToLower().Replace(@"thumbnailurl=&quot;", string.Empty).Replace("&quot;", string.Empty);
                    }
                }

                var attachment = new Attachment(contentType, contentUrl, name: !string.IsNullOrEmpty(name) ? name : null, thumbnailUrl: !string.IsNullOrEmpty(thumbnailUrl) ? thumbnailUrl : null);
                message.Attachments.Add(attachment);
            }

            return message;
        }

        //Uses azure-textanalytics keyphrases to search for keywords
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
    }
}