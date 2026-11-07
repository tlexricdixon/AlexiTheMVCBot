using AlexiTheMVCBot.Code;
using Markdig;
using Standard.AI.OpenAI.Models.Services.Foundations.ChatCompletions;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using TimeZoneConverter;

namespace AlexiTheMVCBot.ChatGpt
{
    public class OpenAIServices : IOpenAiProxy
    {
        readonly List<ChatCompletionMessage> _messages;
        readonly HttpClient _httpClient;
        readonly string _model;
        const string _botName = "Alexi";

        public OpenAIServices(IConfiguration configuration)
        {
            _model = configuration.GetValue<string>("Ollama:Model") ?? "ministral-3:8b";
            var baseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434";

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };

            _messages = [];
        }

        void StackMessages(params ChatCompletionMessage[] messages)
        {
            foreach (var message in messages)
            {
                if (message.Content.Contains("I’m called ChatGPT."))
                {
                    message.Content = message.Content.Replace("I’m called ChatGPT.", $"I’m called {_botName}.");
                }

                _messages.Add(message);
            }
        }

        public Task<ChatCompletionMessage[]> SendChatMessage(string prompt)
        {
            var chatMsg = new ChatCompletionMessage()
            {
                Content = prompt,
                Role = "user"
            };

            if (prompt.Contains(@"What time is it", StringComparison.OrdinalIgnoreCase))
            {
                TimeZoneInfo dallasTimeZone = TZConvert.GetTimeZoneInfo("Central Standard Time");
                DateTime dallasTime = TimeZoneInfo.ConvertTime(DateTime.Now, dallasTimeZone);
                Console.WriteLine("The current time in Dallas, TX is: " + dallasTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            }

            return SendChatMessage(chatMsg);
        }

        async Task<ChatCompletionMessage[]> SendChatMessage(ChatCompletionMessage message)
        {
            foreach (var x in _messages)
            {
                if (x.Role == _botName)
                {
                    x.Role = "assistant";
                }
            }

            StackMessages(message);

            var request = new OllamaChatRequest
            {
                Model = _model,
                Stream = false,
                Messages = _messages
                    .Select(x => new OllamaChatMessage
                    {
                        Role = x.Role == _botName ? "assistant" : x.Role,
                        Content = x.Content
                    })
                    .ToList()
            };

            using var response = await _httpClient.PostAsJsonAsync("/api/chat", request);
            response.EnsureSuccessStatusCode();

            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
            var assistantContent = ollamaResponse?.Message?.Content ?? string.Empty;

            var assistantMessage = new ChatCompletionMessage
            {
                Role = "assistant",
                Content = assistantContent
            };

            string markdownResponse = assistantMessage.Content;
            var sb = new StringBuilder();
            sb.Append(Markdown.ToHtml(markdownResponse));
            sb.Append(" User: ");
            sb.Append(assistantMessage.Content);
            assistantMessage.Content = sb.ToString();

            StackMessages(assistantMessage);

            return [assistantMessage];
        }

        sealed class OllamaChatRequest
        {
            public string Model { get; set; } = string.Empty;
            public bool Stream { get; set; }
            public List<OllamaChatMessage> Messages { get; set; } = [];
        }

        sealed class OllamaChatMessage
        {
            public string Role { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        sealed class OllamaChatResponse
        {
            public OllamaChatMessage? Message { get; set; }
        }
    }
}
