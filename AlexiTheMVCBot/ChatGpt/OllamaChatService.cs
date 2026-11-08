using AlexiTheMVCBot.Code;
using AlexiTheMVCBot.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AlexiTheMVCBot.ChatGpt
{
    // Scoped to a Blazor Server circuit: never share a listener's chat history globally.
    public sealed class OllamaChatService : IChatProxy, IDisposable
    {
        private const int MaxInputLength = 1000;
        private const int MaxHistoryMessages = 20;

        private readonly List<ChatMessage> _messages = [];
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly bool _think;
        private readonly OllamaOptions _options;

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
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(90)
            };
        }

        public async Task<ChatMessage[]> SendChatMessage(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > MaxInputLength)
                throw new ArgumentException("Message must be between 1 and 1000 characters.", nameof(prompt));

            var history = _messages
                .TakeLast(MaxHistoryMessages - 1)
                .Select(x => new OllamaChatMessage { Role = x.Role, Content = x.Content })
                .ToList();
            history.Add(new OllamaChatMessage { Role = "user", Content = prompt.Trim() });

            var request = new OllamaChatRequest
            {
                Model = _model,
                Stream = false,
                Think = _think,
                Options = _options,
                Messages = history
            };

            using var response = await _httpClient.PostAsJsonAsync("/api/chat", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
            var answer = result?.Message?.Content;
            if (string.IsNullOrWhiteSpace(answer))
                throw new InvalidOperationException("The AI service returned an empty response.");

            // Commit history only after success. Store raw text; Razor encodes it on display.
            _messages.Add(new ChatMessage { Role = "user", Content = prompt.Trim() });
            _messages.Add(new ChatMessage { Role = "assistant", Content = answer });
            if (_messages.Count > MaxHistoryMessages)
                _messages.RemoveRange(0, _messages.Count - MaxHistoryMessages);

            return [new ChatMessage { Role = "assistant", Content = answer }];
        }

        public void Dispose() => _httpClient.Dispose();

        private sealed class OllamaChatRequest
        {
            public string Model { get; set; } = string.Empty;
            public bool Stream { get; set; }

            [JsonPropertyName("think")]
            public bool Think { get; set; }

            [JsonPropertyName("options")]
            public OllamaOptions Options { get; set; } = new();

            public List<OllamaChatMessage> Messages { get; set; } = [];
        }

        private sealed class OllamaOptions
        {
            [JsonPropertyName("num_ctx")]
            public int NumCtx { get; set; } = 4096;

            [JsonPropertyName("num_predict")]
            public int NumPredict { get; set; } = 180;

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; } = 0.8;
        }

        private sealed class OllamaChatMessage
        {
            public string Role { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        private sealed class OllamaChatResponse
        {
            public OllamaChatMessage? Message { get; set; }
        }
    }
}
