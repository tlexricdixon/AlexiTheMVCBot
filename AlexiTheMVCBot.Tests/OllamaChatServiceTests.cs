using AlexiTheMVCBot.ChatGpt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AlexiTheMVCBot.Tests;

public sealed class OllamaChatServiceTests
{
    [Fact]
    public async Task SeparateServiceInstancesKeepIndependentVisitorConversations()
    {
        var handler = new SequenceHandler(
            CreateJsonResponse("one"),
            CreateJsonResponse("two"));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var config = CreateConfiguration();

        using var visitorA = new OllamaChatService(config, NullLogger<OllamaChatService>.Instance, httpClient);
        using var visitorB = new OllamaChatService(config, NullLogger<OllamaChatService>.Instance, httpClient);

        await visitorA.SendChatMessage("hello", "kathleen");
        await visitorB.SendChatMessage("hello", "kathleen");

        Assert.Equal(2, visitorA.GetConversation("kathleen").Count);
        Assert.Equal(2, visitorB.GetConversation("kathleen").Count);
        Assert.Equal("one", visitorA.GetConversation("kathleen").Last().Content);
        Assert.Equal("two", visitorB.GetConversation("kathleen").Last().Content);
    }

    [Fact]
    public async Task SeparateDjHistoriesDoNotMix()
    {
        var handler = new SequenceHandler(
            CreateJsonResponse("kathleen-response"),
            CreateJsonResponse("lexi-response"));

        using var service = new OllamaChatService(CreateConfiguration(), NullLogger<OllamaChatService>.Instance, new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") });

        await service.SendChatMessage("hello", "kathleen");
        await service.SendChatMessage("yo", "lexi");

        var kathleenHistory = service.GetConversation("kathleen");
        var lexiHistory = service.GetConversation("lexi");

        Assert.Equal(2, kathleenHistory.Count);
        Assert.Equal(2, lexiHistory.Count);
        Assert.DoesNotContain(lexiHistory, m => m.Content.Contains("kathleen-response", StringComparison.Ordinal));
        Assert.DoesNotContain(kathleenHistory, m => m.Content.Contains("lexi-response", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailureDoesNotPersistConversationTurn()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            CreateJsonResponse("recovered"));

        using var service = new OllamaChatService(CreateConfiguration(), NullLogger<OllamaChatService>.Instance, new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") });

        await Assert.ThrowsAsync<ChatRequestFailedException>(() => service.SendChatMessage("first", "kathleen"));
        Assert.Empty(service.GetConversation("kathleen"));

        await service.SendChatMessage("first", "kathleen");
        var history = service.GetConversation("kathleen");

        Assert.Equal(2, history.Count);
        Assert.Equal("first", history[0].Content);
    }

    [Fact]
    public async Task TimeoutReturnsFriendlyMessageAndDoesNotPersistTurn()
    {
        var config = CreateConfiguration(requestTimeoutSeconds: 1);
        var handler = new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            return CreateJsonResponse("late");
        });

        using var service = new OllamaChatService(config, NullLogger<OllamaChatService>.Instance, new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") });

        var ex = await Assert.ThrowsAsync<ChatRequestFailedException>(() => service.SendChatMessage("slow", "kathleen"));

        Assert.Equal("Your DJ is taking too long to respond. Please try again.", ex.UserMessage);
        Assert.Empty(service.GetConversation("kathleen"));
    }

    [Fact]
    public async Task MalformedResponseReturnsFriendlyErrorAndDoesNotPersistTurn()
    {
        var malformed = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        using var service = new OllamaChatService(CreateConfiguration(), NullLogger<OllamaChatService>.Instance, new HttpClient(new SequenceHandler(malformed)) { BaseAddress = new Uri("http://localhost:11434") });

        var ex = await Assert.ThrowsAsync<ChatRequestFailedException>(() => service.SendChatMessage("hello", "kathleen"));

        Assert.Equal("Your DJ sent an empty response. Please try again.", ex.UserMessage);
        Assert.Empty(service.GetConversation("kathleen"));
    }

    [Fact]
    public async Task ConversationHistoryIsTrimmedToMaxStoredMessages()
    {
        var responses = Enumerable.Range(0, 60)
            .Select(i => CreateJsonResponse($"answer-{i}"))
            .ToArray();

        using var service = new OllamaChatService(CreateConfiguration(), NullLogger<OllamaChatService>.Instance, new HttpClient(new SequenceHandler(responses)) { BaseAddress = new Uri("http://localhost:11434") });

        for (var i = 0; i < 60; i++)
        {
            await service.SendChatMessage($"message-{i}", "kathleen");
        }

        var history = service.GetConversation("kathleen", 200);

        Assert.Equal(80, history.Count);
        Assert.Equal("message-20", history[0].Content);
        Assert.Equal("answer-59", history[^1].Content);
    }

    private static IConfiguration CreateConfiguration(int requestTimeoutSeconds = 45)
    {
        var values = new Dictionary<string, string?>
        {
            ["Ollama:BaseUrl"] = "http://localhost:11434",
            ["Ollama:Model"] = "qwen3.5:2b",
            ["Ollama:RequestTimeoutSeconds"] = requestTimeoutSeconds.ToString(),
            ["Ollama:Options:NumCtx"] = "4096",
            ["Ollama:Options:NumPredict"] = "180",
            ["Ollama:Options:Temperature"] = "0.8"
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static HttpResponseMessage CreateJsonResponse(string assistantContent)
    {
        var payload = JsonSerializer.Serialize(new
        {
            message = new
            {
                role = "assistant",
                content = assistantContent
            },
            total_duration = 1,
            load_duration = 1,
            prompt_eval_duration = 1,
            eval_duration = 1,
            prompt_eval_count = 1,
            eval_count = 1
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException("No mock response queued.");

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            sendAsync(request, cancellationToken);
    }
}
