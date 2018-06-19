using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace OmnBotQnamaker.Global
{
    public class Rootobject
    {
        public Document[] documents { get; set; }
    }

    public class Document
    {
        public string language { get; set; }
        public string id { get; set; }
        public string text { get; set; }
    }

    public class DocumentResponse
    {
        [JsonProperty(PropertyName = "documents")]
        public ResponseDocument[] documents { get; set; }
    }

    public class ResponseDocument
    {
        public int id { get; set; }

        public string[] keyPhrases { get; set; }
    }

    public class TextAnalytics
    {
        static string host = "https://northeurope.api.cognitive.microsoft.com";
        static string path = "/text/analytics/v2.0/keyPhrases";

        // NOTE: Replace this example key with a valid subscription key.
        static string key = ConfigurationManager.AppSettings["TextAnalyticsKey"];

        public static async Task<DocumentResponse> GetKeyphrases(string userQuery)
        {
            Document[] docs = { new Document { language = "en", id = "1", text =  userQuery } };
            var body = new Rootobject { documents = docs };

            string uri = host + path ;
            var requestBody = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", key);

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<DocumentResponse>(responseBody);
                return result;
            }
        }
    }
}