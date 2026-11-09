using AlexiTheMVCBot.ChatGpt;
using AlexiTheMVCBot.Code;
using AlexiTheMVCBot.Components.Pages;
using AlexiTheMVCBot.Models;
using AlexiTheMVCBot.Tests.TestSupport;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace AlexiTheMVCBot.Tests;

public sealed class HomeComponentTests
{
    [Fact]
    public void SwitchingDjsKeepsHistoriesIndependent()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var proxy = new FakeChatProxy();
        ctx.Services.AddSingleton<IChatProxy>(proxy);

        var cut = ctx.Render<Home>();

        cut.Find(".chat-launcher").Click();
        cut.Find("#chat-input").Change("hello kathleen");
        cut.Find(".chat-input-row").Submit();

        cut.WaitForAssertion(() => Assert.Contains("kathleen:hello kathleen", cut.Markup));

        cut.Find("#dj-picker").Change("lexi");
        Assert.DoesNotContain("kathleen:hello kathleen", cut.Markup);

        cut.Find("#chat-input").Change("hello lexi");
        cut.Find(".chat-input-row").Submit();

        cut.WaitForAssertion(() => Assert.Contains("lexi:hello lexi", cut.Markup));
        Assert.DoesNotContain("kathleen:hello kathleen", cut.Markup);

        cut.Find("#dj-picker").Change("kathleen");
        Assert.Contains("kathleen:hello kathleen", cut.Markup);
        Assert.DoesNotContain("lexi:hello lexi", cut.Markup);
    }

    [Fact]
    public void FailedMessagePreservesInputAndRetryDoesNotDuplicate()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var proxy = new FakeChatProxy();
        var firstAttempt = true;
        proxy.OnSendAsync = (_, _, _) =>
        {
            if (firstAttempt)
            {
                firstAttempt = false;
                throw new ChatRequestFailedException(
                    "Your DJ couldn't respond right now. Please try again shortly.",
                    "simulated");
            }

            return Task.FromResult(new[] { new ChatMessage { Role = "assistant", Content = "recovered" } });
        };

        ctx.Services.AddSingleton<IChatProxy>(proxy);

        var cut = ctx.Render<Home>();
        cut.Find(".chat-launcher").Click();
        cut.Find("#chat-input").Change("retry me");
        cut.Find(".chat-input-row").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Your DJ couldn't respond right now. Please try again shortly.", cut.Markup));

        cut.Find(".chat-input-row").Submit();

        cut.WaitForAssertion(() => Assert.Contains("recovered", cut.Markup));

        var matchingUserMessages = cut.FindAll(".chat-message.from-user p")
            .Count(node => string.Equals(node.TextContent, "retry me", StringComparison.Ordinal));
        Assert.Equal(1, matchingUserMessages);
        Assert.Equal(new[] { "retry me", "retry me" }, proxy.SentPrompts);
    }

    [Fact]
    public void RejectsOversizedInputWithoutCallingProxy()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var proxy = new FakeChatProxy();
        ctx.Services.AddSingleton<IChatProxy>(proxy);

        var cut = ctx.Render<Home>();
        cut.Find(".chat-launcher").Click();

        var oversized = new string('x', 1001);
        cut.Find("#chat-input").Change(oversized);
        cut.Find(".chat-input-row").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Please keep your message under 1,000 characters.", cut.Markup));
        Assert.Equal(0, proxy.SendCallCount);
    }

    [Fact]
    public void ModelOutputIsRenderedSafelyAsText()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var proxy = new FakeChatProxy
        {
            OnSendAsync = (_, _, _) => Task.FromResult(new[]
            {
                new ChatMessage { Role = "assistant", Content = "<script>alert('xss')</script> hello" }
            })
        };
        ctx.Services.AddSingleton<IChatProxy>(proxy);

        var cut = ctx.Render<Home>();
        cut.Find(".chat-launcher").Click();
        cut.Find("#chat-input").Change("test");
        cut.Find(".chat-input-row").Submit();

        cut.WaitForAssertion(() => Assert.Contains("alert('xss')", cut.Markup));
        Assert.Empty(cut.FindAll("script"));
    }
}
