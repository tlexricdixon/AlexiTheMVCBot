using AlexiTheMVCBot.Djs;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AlexiTheMVCBot.ChatGpt;

// Development-only, server-wide proof of concept. Not a public chat or durable message store.
// One controlled run produces at most three DJ replies; there is no autonomous loop.
public sealed class DjGroupRoom : IDisposable
{
    private const int MaxVisibleMessages = 32;
    private const int ModelHistoryMessages = 12;
    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private readonly List<GroupLine> _lines = [];
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly bool _think;
    private readonly bool _enabled;
    private int _nextSpeaker;

    public event Action? Changed;

    public DjGroupRoom(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _enabled = environment.IsDevelopment();
        _model = configuration.GetValue<string>("Ollama:Model") ?? "qwen3.5:2b";
        _think = configuration.GetValue<bool?>("Ollama:Think") ?? false;
        _http = new HttpClient
        {
            BaseAddress = new Uri(configuration.GetValue<string>("Ollama:BaseUrl") ?? "http://localhost:11434"),
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public bool Enabled => _enabled;
    public bool IsRunning { get; private set; }
    public IReadOnlyList<GroupLine> Snapshot()
    {
        lock (_sync) return _lines.ToArray();
    }

    public async Task RunThreeTurnsAsync()
    {
        if (!_enabled) throw new InvalidOperationException("The group experiment is only enabled in Development.");
        if (!await _runGate.WaitAsync(0)) return;

        try
        {
            IsRunning = true;
            Changed?.Invoke();
            for (var i = 0; i < 3; i++)
            {
                var profile = DjProfiles.All[_nextSpeaker % DjProfiles.All.Count];
                IReadOnlyList<GroupLine> recent = Snapshot().TakeLast(ModelHistoryMessages).ToArray();
                var messages = new List<OllamaMessage>
                {
                    new("system", DjProfiles.BuildSystemPrompt(profile) + """

                        
                        This is a fictional group radio-studio conversation, not a private chat.
                        You are speaking to the other DJs, not to a visitor.
                        Respond ONLY as your own named character. Do not write dialogue for anyone else.
                        React to the most recent remark and add one fresh thought or playful disagreement.
                        Keep it to one or two sentences, under 50 words. Avoid repetitive greetings.
                        All eight DJs are fictional AI characters; never claim real-world experiences.
                        """)
                };

                if (recent.Count == 0)
                {
                    messages.Add(new("user",
                        "Studio rehearsal: The station is finally giving all eight DJs one microphone. " +
                        "Start a playful discussion about whether the radio station should have purple lights."));
                }
                else
                {
                    // Other speakers are represented as quoted transcript, never as prior
                    // assistant turns from this character's own conversation.
                    var transcript = string.Join("\n", recent.Select(x => x.Speaker + ": " + x.Text));
                    messages.Add(new("user",
                        "Recent studio conversation:\n" + transcript +
                        "\n\nContinue naturally as " + profile.Name + " only."));
                }

                using var response = await _http.PostAsJsonAsync("/api/chat", new OllamaRequest(
                    _model, false, _think, "30m",
                    new OllamaOptions(4096, 100, 0.85), messages));
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                var answer = result?.Message?.Content?.Trim();
                if (string.IsNullOrWhiteSpace(answer))
                    throw new InvalidOperationException("Ollama returned an empty group reply.");

                lock (_sync)
                {
                    _lines.Add(new GroupLine(profile.Id, profile.Name, answer, DateTimeOffset.UtcNow));
                    if (_lines.Count > MaxVisibleMessages)
                        _lines.RemoveRange(0, _lines.Count - MaxVisibleMessages);
                    _nextSpeaker++;
                }
                Changed?.Invoke();
            }
        }
        finally
        {
            IsRunning = false;
            Changed?.Invoke();
            _runGate.Release();
        }
    }

    public void Reset()
    {
        if (!_enabled) return;
        if (!_runGate.Wait(0)) return;
        try
        {
            lock (_sync)
            {
                _lines.Clear();
                _nextSpeaker = 0;
            }
            Changed?.Invoke();
        }
        finally { _runGate.Release(); }
    }

    public void Dispose()
    {
        _http.Dispose();
        _runGate.Dispose();
    }

    public sealed record GroupLine(string DjId, string Speaker, string Text, DateTimeOffset At);
    private sealed record OllamaMessage(string Role, string Content);
    private sealed record OllamaRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("think")] bool Think,
        [property: JsonPropertyName("keep_alive")] string KeepAlive,
        [property: JsonPropertyName("options")] OllamaOptions Options,
        [property: JsonPropertyName("messages")] List<OllamaMessage> Messages);
    private sealed record OllamaOptions(
        [property: JsonPropertyName("num_ctx")] int NumCtx,
        [property: JsonPropertyName("num_predict")] int NumPredict,
        [property: JsonPropertyName("temperature")] double Temperature);
    private sealed record OllamaResponse(OllamaMessage? Message);
}
