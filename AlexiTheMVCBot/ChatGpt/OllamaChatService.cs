using AlexiTheMVCBot.Code;
using AlexiTheMVCBot.Models;
using Markdig;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AlexiTheMVCBot.ChatGpt
{
    public class OllamaChatService : IChatProxy
    {
        readonly List<ChatMessage> _messages;
        readonly HttpClient _httpClient;
        readonly string _model;
        readonly bool _think;
        readonly OllamaOptions _options;

        public OllamaChatService(IConfiguration configuration)
        {
            _model = configuration.GetValue<string>("Ollama:Model") ?? "ministral-3:8b";
            var baseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434";
            _think = configuration.GetValue<bool?>("Ollama:Think") ?? false;
            _options = new OllamaOptions
            {
                NumCtx = configuration.GetValue<int?>("Ollama:Options:NumCtx") ?? 4096,
                NumPredict = configuration.GetValue<int?>("Ollama:Options:NumPredict") ?? 180,
                Temperature = configuration.GetValue<double?>("Ollama:Options:Temperature") ?? 0.8
            };

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };

            _messages = [];
        }

        void StackMessages(params ChatMessage[] messages)
        {
            foreach (var message in messages)
            {
                _messages.Add(message);
            }
        }

        public Task<ChatMessage[]> SendChatMessage(string prompt)
        {
            var chatMsg = new ChatMessage
            {
                Content = prompt,
                Role = "user"
            };

            return SendChatMessage(chatMsg);
        }

        async Task<ChatMessage[]> SendChatMessage(ChatMessage message)
        {
            StackMessages(message);

            var request = new OllamaChatRequest
            {
                Model = _model,
                Stream = false,
                Think = _think,
                Options = _options,
                Messages = _messages
                    .Select(x => new OllamaChatMessage
                    {
                        Role = x.Role,
                        Content = x.Content
                    })
                    .ToList()
            };

            using var response = await _httpClient.PostAsJsonAsync("/api/chat", request);
            response.EnsureSuccessStatusCode();

            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
            var assistantContent = ollamaResponse?.Message?.Content ?? string.Empty;

            var assistantResponse = new ChatMessage
            {
                Role = "assistant",
                Content = Markdown.ToHtml(assistantContent)
            };

            StackMessages(new ChatMessage
            {
                Role = "assistant",
                Content = assistantContent
            });

            return [assistantResponse];
        }

        sealed class OllamaChatRequest
        {
            public string Model { get; set; } = string.Empty;
            public bool Stream { get; set; }

            [JsonPropertyName("think")]
            public bool Think { get; set; }

            [JsonPropertyName("options")]
            public OllamaOptions Options { get; set; } = new();

            public List<OllamaChatMessage> Messages { get; set; } = [];
        }

        sealed class OllamaOptions
        {
            [JsonPropertyName("num_ctx")]
            public int NumCtx { get; set; } = 4096;

            [JsonPropertyName("num_predict")]
            public int NumPredict { get; set; } = 180;

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; } = 0.8;
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
