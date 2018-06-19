using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Builder.Dialogs;
using Newtonsoft.Json;
using OmnBotQnamaker.Dialogs.QnADialog.Models;

namespace OmnBotQnamaker.Dialogs.QnADialog
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
    [Serializable]
    public class QnAService : Attribute
    {
        public string BaseUri { get; set; }
        public string EndpointKey { get; set; }
        public string KnowledgeBaseId { get; set; }
        public string SubscriptionKey { get; set; }
        public int MaxAnswers { get; set; }
        public List<Metadata> MetadataBoost { get; set; }
        public List<Metadata> MetadataFilter { get; set; }

        public QnAService(string baseUri, string EndpointKey, string KnowledgeBaseId, string SubscriptionKey, int MaxAnswers = 5, List<Metadata> strictFilter = null) {
            this.BaseUri = baseUri;
            this.EndpointKey = EndpointKey;
            this.SubscriptionKey = SubscriptionKey;
            this.KnowledgeBaseId = KnowledgeBaseId;
            this.MaxAnswers = MaxAnswers;
            this.MetadataFilter = strictFilter;
        }

        public async Task<QuestionResponse.QnAMakerResult> GetQnAMakerResponse(string query)
        {
            string responseString;
            var qnamakerBaseUri = this.BaseUri;
            var knowledgebaseId = this.KnowledgeBaseId; // Use knowledge base id created.
            var qnamakerEndpointKey = this.EndpointKey; //Use endpoint key assigned to you.

            //Build the URI
            var qnamakerUriBase = new Uri(qnamakerBaseUri);
            var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");

            //Add the question as part of the body
            var request = new QuestionRequest()
            {
                Question = query,
                Top = MaxAnswers,
                UserId = "QnAMakerDialog"
            };

            request.MetadataBoost = MetadataBoost?.ToArray() ?? new Models.Metadata[] { };
            request.StrictFilters = MetadataFilter?.ToArray() ?? new Models.Metadata[] { };

            var postBody = JsonConvert.SerializeObject(request);

            //Send the POST request
            using (WebClient client = new WebClient())
            {
                //Set the encoding to UTF8
                client.Encoding = System.Text.Encoding.UTF8;

                //Add the subscription key header
                client.Headers.Add("Authorization", $"EndpointKey {qnamakerEndpointKey}");
                client.Headers.Add("Content-Type", "application/json");
                try
                {
                    responseString = client.UploadString(builder.Uri, postBody);
                }
                catch (WebException err)
                {
                    throw new Exception(err.Message);
                }
            }

            //De-serialize the response
            try
            {
                var response = JsonConvert.DeserializeObject<QuestionResponse.QnAMakerResult>(responseString);
                return response;
            }
            catch
            {
                throw new Exception("Unable to deserialize QnA Maker response string.");
            }
        }

        public async Task UpdateKnowledgebase(KbUpdateRequest requestObject)
        {
            string responseString;
            var qnamakerBaseUri = this.BaseUri;
            var knowledgebaseId = this.KnowledgeBaseId; // Use knowledge base id created.
            var qnamakerEndpointKey = this.EndpointKey; //Use endpoint key assigned to you.

            //Build the URI
            var updateBaseEndpoint = "https://westus.api.cognitive.microsoft.com/qnamaker/v4.0/";
            var qnamakerUriBase = new Uri(qnamakerBaseUri);
            var builder = new UriBuilder($"{updateBaseEndpoint}/knowledgebases/{knowledgebaseId}");

            var postBody = JsonConvert.SerializeObject(requestObject);

        

            //Send the POST request
            using (WebClient client = new WebClient())
            {
                //Set the encoding to UTF8
                client.Encoding = System.Text.Encoding.UTF8;
                //Add the subscription key header
                client.Headers.Add("Ocp-Apim-Subscription-Key", $"{this.SubscriptionKey}");
                client.Headers.Add("Content-Type", "application/json");
                try
                {
                    responseString = client.UploadString(builder.Uri, "PATCH", postBody);
                }
                catch (WebException err)
                {
                    throw new Exception(err.Message);
                }
            }
        }
    }
}