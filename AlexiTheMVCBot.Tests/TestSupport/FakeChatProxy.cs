using AlexiTheMVCBot.ChatGpt;
using AlexiTheMVCBot.Code;
using AlexiTheMVCBot.Models;

namespace AlexiTheMVCBot.Tests.TestSupport;

internal sealed class FakeChatProxy : IChatProxy
{
    private readonly Dictionary<string, List<ChatMessage>> _conversations = new(StringComparer.OrdinalIgnoreCase);

    public int SendCallCount { get; private set; }
    public List<string> SentPrompts { get; } = [];

    public Func<string, string, CancellationToken, Task<ChatMessage[]>>? OnSendAsync { get; set; }

    public IReadOnlyList<ChatMessage> GetConversation(string djId = "kathleen", int maxMessages = 40)
    {
        if (!_conversations.TryGetValue(djId, out var history) || maxMessages <= 0)
            return [];

        return history.TakeLast(maxMessages).ToArray();
    }

    public async Task<ChatMessage[]> SendChatMessage(string prompt, string djId = "kathleen", CancellationToken cancellationToken = default)
    {
        SendCallCount++;
        SentPrompts.Add(prompt);

        if (OnSendAsync is not null)
        {
            var custom = await OnSendAsync(prompt, djId, cancellationToken);
            if (custom.Length > 0)
            {
                AppendSuccessfulTurn(djId, prompt, custom[0].Content);
            }

            return custom;
        }

        var responseText = $"{djId}:{prompt}";
        AppendSuccessfulTurn(djId, prompt, responseText);
        return [new ChatMessage { Role = "assistant", Content = responseText }];
    }

    private void AppendSuccessfulTurn(string djId, string prompt, string assistantResponse)
    {
        if (!_conversations.TryGetValue(djId, out var history))
        {
            history = [];
            _conversations[djId] = history;
        }

        history.Add(new ChatMessage { Role = "user", Content = prompt });
        history.Add(new ChatMessage { Role = "assistant", Content = assistantResponse });
    }
}
