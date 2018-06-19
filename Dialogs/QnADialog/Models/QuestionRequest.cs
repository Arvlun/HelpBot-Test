using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OmnBotQnamaker.Dialogs.QnADialog.Models
{
    [Serializable]
    public class QuestionRequest
    {
        [JsonProperty(PropertyName = "question")]
        public string Question { get; set; }

        [JsonProperty(PropertyName = "top")]
        public int Top { get; set; }

        [JsonProperty(PropertyName = "strictFilters")]
        public Metadata[] StrictFilters { get; set; }

        [JsonProperty(PropertyName = "metadataBoost")]
        public Metadata[] MetadataBoost { get; set; }

        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }
    }
}