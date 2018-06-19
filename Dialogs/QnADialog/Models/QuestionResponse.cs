using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OmnBotQnamaker.Dialogs.QnADialog.Models
{
    public class QuestionResponse
    {
        [Serializable]
        public class QnAMakerResult
        {
            [JsonProperty(PropertyName = "answers")]
            public QnaAnswer[] Answers { get; set; }
        }

        [Serializable]
        public class QnaAnswer
        {
            [JsonProperty(PropertyName = "score")]
            public float Score { get; set; }

            [JsonProperty(PropertyName = "id")]
            public int QnaId { get; set; }

            [JsonProperty(PropertyName = "answer")]
            public string Answer { get; set; }

            [JsonProperty(PropertyName = "source")]
            public string Source { get; set; }

            [JsonProperty(PropertyName = "questions")]
            public string[] Questions { get; set; }

            [JsonProperty(PropertyName = "metadata")]
            public Metadata[] Metadata { get; set; }
        }
    }
}