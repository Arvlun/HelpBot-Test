using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OmnBotQnamaker.Dialogs.QnADialog.Models
{
    [Serializable]
    public class KbUpdateRequest
    {
        public ItemsToUpdate update { get; set; }
    }

    [Serializable]
    public class ItemsToUpdate
    {
        //public string name { get; set; }
        [JsonProperty(PropertyName = "qnaList")]
        public List<KbItemToUpdate> QnaList { get; set; }
        //public string[] urls { get; set; }
    }

    [Serializable]
    public class QuestionsUpdateModel
    {
        public string[] add { get; set; }
        //public string[] delete { get; set; }
    }

    [Serializable]
    public class KbItemToUpdate
    {
        //public string answer { get; set; }
        //public string source { get; set; }

        [JsonProperty(PropertyName = "id")]
        public int qnaId { get; set; }
        public QuestionsUpdateModel questions { get; set; }
    }
}