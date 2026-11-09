using AlexiTheMVCBot.Code;
using AlexiTheMVCBot.Djs;
using AlexiTheMVCBot.Models;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AlexiTheMVCBot.ChatGpt
{
    public sealed class ChatRequestFailedException : Exception
    {
        public ChatRequestFailedException(string userMessage, string diagnosticMessage, Exception? innerException = null)
            : base(diagnosticMessage, innerException)
        {
            UserMessage = userMessage;
        }

        public string UserMessage { get; }
    }

    // Scoped to a Blazor Server circuit. Each DJ has a distinct history within that circuit.
    public sealed class OllamaChatService : IChatProxy, IDisposable
    {
        private const int MaxInputLength = 1000;
        private const int MaxContextMessages = 20;
        private const int MaxStoredMessages = 80;
        private readonly Dictionary<string, List<ChatMessage>> _histories = new(StringComparer.OrdinalIgnoreCase);
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaChatService> _logger;
        private readonly string _model;
        private readonly bool _think;
        private readonly OllamaOptions _options;
        private readonly TimeSpan _requestTimeout;

        public OllamaChatService(IConfiguration configuration, ILogger<OllamaChatService> logger)
            : this(configuration, logger, httpClient: null)
        {
        }

        public OllamaChatService(IConfiguration configuration, ILogger<OllamaChatService> logger, HttpClient? httpClient)
        {
            _logger = logger;
            _model = configuration.GetValue<string>("Ollama:Model") ?? "qwen3.5:2b";
            var baseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434";
            _think = configuration.GetValue<bool?>("Ollama:Think") ?? false;
            _requestTimeout = TimeSpan.FromSeconds(configuration.GetValue<int?>("Ollama:RequestTimeoutSeconds") ?? 45);
            _options = new OllamaOptions
            {
                NumCtx = configuration.GetValue<int?>("Ollama:Options:NumCtx") ?? 4096,
                NumPredict = configuration.GetValue<int?>("Ollama:Options:NumPredict") ?? 180,
                Temperature = configuration.GetValue<double?>("Ollama:Options:Temperature") ?? 0.8
            };
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.BaseAddress ??= new Uri(baseUrl);
            _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        }

        public IReadOnlyList<ChatMessage> GetConversation(string djId = "kathleen", int maxMessages = 40)
        {
            var profile = DjProfiles.Get(djId);
            if (!_histories.TryGetValue(profile.Id, out var history))
                return [];

            if (maxMessages <= 0)
                return [];

            return history.TakeLast(maxMessages).ToArray();
        }

        public async Task<ChatMessage[]> SendChatMessage(string prompt, string djId = "kathleen", CancellationToken cancellationToken = default)
        {
            var trimmedPrompt = prompt?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedPrompt) || trimmedPrompt.Length > MaxInputLength)
                throw new ArgumentException("Message must be between 1 and 1000 characters.", nameof(prompt));

            var profile = DjProfiles.Get(djId);
            if (!_histories.TryGetValue(profile.Id, out var history))
            {
                history = [];
                _histories[profile.Id] = history;
            }

            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = DjProfiles.BuildSystemPrompt(profile) }
            };
            messages.AddRange(history.TakeLast(MaxContextMessages)
                .Select(x => new OllamaChatMessage { Role = x.Role, Content = x.Content }));
            messages.Add(new OllamaChatMessage { Role = "user", Content = trimmedPrompt });

            var request = new OllamaChatRequest
            {
                Model = _model,
                Stream = false,
                Think = _think,
                Options = _options,
                Messages = messages
            };

            var stopwatch = Stopwatch.StartNew();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_requestTimeout);

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("/api/chat", request, timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama returned non-success status {StatusCode} for DJ {DjId}.", (int)response.StatusCode, profile.Id);
                    throw new ChatRequestFailedException(
                        "Your DJ couldn't respond right now. Please try again shortly.",
                        $"Ollama returned status {(int)response.StatusCode} for DJ '{profile.Id}'.");
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: timeoutCts.Token);
                var answer = result?.Message?.Content?.Trim();
                if (string.IsNullOrWhiteSpace(answer))
                {
                    _logger.LogWarning("Ollama returned an empty or malformed response for DJ {DjId}.", profile.Id);
                    throw new ChatRequestFailedException(
                        "Your DJ sent an empty response. Please try again.",
                        $"Ollama response was empty or malformed for DJ '{profile.Id}'.");
                }

                // Only save successful turns, and never share private histories across DJs/circuits.
                history.Add(new ChatMessage { Role = "user", Content = trimmedPrompt });
                history.Add(new ChatMessage { Role = "assistant", Content = answer });
                if (history.Count > MaxStoredMessages)
                    history.RemoveRange(0, history.Count - MaxStoredMessages);

                var metrics = result ?? new OllamaChatResponse();
                _logger.LogInformation(
                    "Ollama timing for DJ {DjId}: elapsedMs={ElapsedMs}, total_duration={TotalDuration}, load_duration={LoadDuration}, prompt_eval_duration={PromptEvalDuration}, eval_duration={EvalDuration}, prompt_eval_count={PromptEvalCount}, eval_count={EvalCount}",
                    profile.Id,
                    stopwatch.ElapsedMilliseconds,
                    metrics.TotalDuration,
                    metrics.LoadDuration,
                    metrics.PromptEvalDuration,
                    metrics.EvalDuration,
                    metrics.PromptEvalCount,
                    metrics.EvalCount);

                return [new ChatMessage { Role = "assistant", Content = answer }];
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Ollama request timed out for DJ {DjId} after {ElapsedMs} ms.", profile.Id, stopwatch.ElapsedMilliseconds);
                throw new ChatRequestFailedException(
                    "Your DJ is taking too long to respond. Please try again.",
                    $"Timeout waiting for Ollama response for DJ '{profile.Id}'.",
                    ex);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation("Ollama request canceled for DJ {DjId} after {ElapsedMs} ms.", profile.Id, stopwatch.ElapsedMilliseconds);
                throw new ChatRequestFailedException(
                    "Request canceled before your DJ replied. Please try again.",
                    $"Canceled Ollama response for DJ '{profile.Id}'.",
                    ex);
            }
            catch (ChatRequestFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ollama request failed for DJ {DjId} after {ElapsedMs} ms.", profile.Id, stopwatch.ElapsedMilliseconds);
                throw new ChatRequestFailedException(
                    "Your DJ couldn't respond right now. Please try again shortly.",
                    $"Unexpected Ollama failure for DJ '{profile.Id}'.",
                    ex);
            }
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
            [JsonPropertyName("total_duration")]
            public long? TotalDuration { get; set; }
            [JsonPropertyName("load_duration")]
            public long? LoadDuration { get; set; }
            [JsonPropertyName("prompt_eval_duration")]
            public long? PromptEvalDuration { get; set; }
            [JsonPropertyName("eval_duration")]
            public long? EvalDuration { get; set; }
            [JsonPropertyName("prompt_eval_count")]
            public int? PromptEvalCount { get; set; }
            [JsonPropertyName("eval_count")]
            public int? EvalCount { get; set; }
        }
    }
}
