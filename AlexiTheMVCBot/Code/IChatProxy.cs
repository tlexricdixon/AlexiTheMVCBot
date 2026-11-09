using AlexiTheMVCBot.Models;

namespace AlexiTheMVCBot.Code
{
    public interface IChatProxy
    {
        Task<ChatMessage[]> SendChatMessage(string prompt, string djId = "kathleen", CancellationToken cancellationToken = default);
        IReadOnlyList<ChatMessage> GetConversation(string djId = "kathleen", int maxMessages = 40);
    }
}
