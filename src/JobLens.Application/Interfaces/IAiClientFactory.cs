using Microsoft.Extensions.AI;

namespace JobLens.Application.Interfaces;

public interface IAiClientFactory
{
    bool IsConfigured { get; }

    string Label { get; }

    IChatClient CreateClient(bool useFunctionInvocation);
}
