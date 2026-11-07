using Standard.AI.OpenAI.Models.Services.Foundations.ChatCompletions;

namespace AlexiTheMVCBot.Code
{
    public interface IOpenAiProxy
    {
        Task<ChatCompletionMessage[]> SendChatMessage(string prompt);
    }
}
