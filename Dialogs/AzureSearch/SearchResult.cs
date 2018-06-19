using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OmnBotQnamaker.Dialogs.AzureSearch
{
    [Serializable]
    public class SearchResult
    {
        [JsonProperty("@search.score")]
        public double Score { get; set; }
        public string Question { get; set; }
        public int Id { get; set; }
        public int QnaId { get; set; }
        public string Answer { get;  set; }
        public string Keywords { get; set; }
    }

    [Serializable]
    public class Results
    {
        public List<SearchResult> Value { get; set; }

        public Results()
        {
            this.Value = new List<SearchResult>();
        }
    }

    public class UploadItem
    {
        [JsonProperty("@search.action")]
        public string Action { get; set; }
        public string Id { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }
        public string MainQuestion { get; set; }
    }
}