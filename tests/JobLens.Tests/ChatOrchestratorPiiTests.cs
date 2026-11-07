using JobLens.Application.Interfaces;
using JobLens.Application.Services;
using JobLens.Web.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class ChatOrchestratorPiiTests
{
    [Fact]
    public void PrepareQuestion_redacts_pii_before_model_or_demo_path()
    {
        var orchestrator = new ChatOrchestrator(
            new ConfigurationBuilder().Build(),
            new StubAiFactory(),
            new PiiRedactor(),
            new StubHttpClientFactory(),
            NullLogger<ChatOrchestrator>.Instance);

        var prepared = orchestrator.PrepareQuestion(
            "Call jane.doe@example.com at (813) 555-0142 about SSN 123-45-6789");

        Assert.DoesNotContain("jane.doe@example.com", prepared, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123-45-6789", prepared, StringComparison.Ordinal);
        Assert.DoesNotContain("(813) 555-0142", prepared, StringComparison.Ordinal);
        Assert.Contains("j***@example.com", prepared, StringComparison.Ordinal);
        Assert.Contains("***", prepared, StringComparison.Ordinal);
    }

    private sealed class StubAiFactory : IAiClientFactory
    {
        public bool IsConfigured => false;
        public string Label => "Demo";
        public IChatClient CreateClient(bool useFunctionInvocation) =>
            throw new NotSupportedException();
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
