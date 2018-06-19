using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OmnBotQnamaker.Dialogs.QnADialog.Models
{
    [Serializable]
    public class Feedback
    {
        public List<UserFeedback> feedbackPairs { get; set; }
    }

    [Serializable]
    public class UserFeedback
    {
        public string userQuestion { get; set; }
        public int kbQuestionId { get; set; }
        public string userId { get; set; }
    }
}