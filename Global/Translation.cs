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
    public class Translations
    {
        public string text { get; set; }
        public string to { get; set; }
    }

    public class DetectedLanguage
    {
        public string language { get; set; }
        public Double score { get; set; }
    }

    public class TransObject
    {
        public DetectedLanguage detectedLanguage { get; set; }
        public List<Translations> translations { get; set; }
    }

    public class Translation
    {
        static string host = "https://api.cognitive.microsofttranslator.com";
        static string path = "/translate?api-version=3.0";


        // NOTE: Replace this example key with a valid subscription key.
        static string key = ConfigurationManager.AppSettings["TranslationKey"];

        public static async Task<string> TranslateToSwedish(string text)
        {
            string params_ = "&to=sv";
            string uri = host + path + params_;
            System.Object[] body = new System.Object[] { new { Text = text } };
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
                var result = JsonConvert.DeserializeObject<List<TransObject>>(responseBody);
                var translation = result[0].translations[0].text;
                return translation;
            }
        }

        public static async Task<string> TranslateToEnglish(string text)
        {
            string params_ = "&to=en";
            string uri = host + path + params_;
            System.Object[] body = new System.Object[] { new { Text = text } };
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
                var result = JsonConvert.DeserializeObject<List<TransObject>>(responseBody);
                var translation = result[0].translations[0].text;
                return translation;
            }
        }
    }
}