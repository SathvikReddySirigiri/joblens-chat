using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace JobLensChat.Services;

// One place that reads Ai:* and builds the chat client. Chat and the offer/search features share it.
public sealed class AiClientFactory(IConfiguration configuration, ILogger<AiClientFactory> logger)
{
    public bool IsConfigured =>
        (IsProvider("AzureOpenAI") || IsProvider("OpenAICompatible"))
        && !string.IsNullOrWhiteSpace(configuration["Ai:Endpoint"])
        && !string.IsNullOrWhiteSpace(configuration["Ai:Model"])
        && !string.IsNullOrWhiteSpace(configuration["Ai:ApiKey"]);

    public string Label => IsConfigured
        ? $"AI mode · {LabelName}"
        : "Demo mode · rule-based (no AI key)";

    public IChatClient CreateClient(bool useFunctionInvocation)
    {
        var model = configuration["Ai:Model"]!;
        var provider = configuration["Ai:Provider"]!;
        var endpoint = new Uri(NormalizeEndpoint(provider, configuration["Ai:Endpoint"]!));
        logger.LogInformation(
            "AI mode starting. Provider {Provider}, host {Host}, model {Model}",
            provider,
            endpoint.Host,
            model);

        IChatClient client = new OpenAIClient(
                new ApiKeyCredential(configuration["Ai:ApiKey"]!),
                new OpenAIClientOptions { Endpoint = endpoint })
            .GetChatClient(model)
            .AsIChatClient();

        return useFunctionInvocation
            ? client.AsBuilder().UseFunctionInvocation().Build()
            : client;
    }

    private string LabelName
    {
        get
        {
            var displayName = configuration["Ai:DisplayName"];
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            var model = configuration["Ai:Model"];
            return IsProvider("AzureOpenAI") ? $"Azure OpenAI · {model}" : model!;
        }
    }

    private bool IsProvider(string name) =>
        string.Equals(configuration["Ai:Provider"], name, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEndpoint(string provider, string endpoint)
    {
        var trimmed = endpoint.Trim().TrimEnd('/');
        if (provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase)
            && !trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += "/openai/v1";
        }

        return trimmed + "/";
    }
}
