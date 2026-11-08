using AlexiTheMVCBot.Models;

namespace AlexiTheMVCBot.Code
{
    public interface IChatProxy
    {
        Task<ChatMessage[]> SendChatMessage(string prompt, string djId = "kathleen");
    }
}
