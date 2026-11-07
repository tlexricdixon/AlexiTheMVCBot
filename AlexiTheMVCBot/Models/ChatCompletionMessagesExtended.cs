using Standard.AI.OpenAI.Models.Services.Foundations.ChatCompletions;
namespace AlexiTheMVCBot.Models
{
    public class ChatCompletionMessagesExtended : ChatCompletionMessage
    {
        public string SpokenContent { get; set; }
    }
}
