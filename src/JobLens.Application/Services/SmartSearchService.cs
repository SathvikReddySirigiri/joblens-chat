using Microsoft.Extensions.Logging;
using JobLens.Application.Common;
using System.Globalization;
using System.Text.Json;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;
using JobLens.Application.Dtos;
using Microsoft.Extensions.AI;

namespace JobLens.Application.Services;

public sealed class SmartSearchService(IAiClientFactory ai, ICompanyRepository companies, ILogger<SmartSearchService> logger)
{
    private const string SystemPrompt =
        """
        Convert a plain-English job search into JSON only. Do not write SQL.
        Shape: {"keyword":"","companyName":"","state":"","isRemote":null,"isPaid":null,"minPayPerHour":null,"excludeCompaniesWithApprovedReports":false}
        Use null when the user did not ask for that filter. state is a 2-letter US code or null.
        keyword is the job words that are not a company, state, or pay filter. Keep keyword under 80 characters.
        """;

    public async Task<SearchFilter> InterpretAsync(string query, CancellationToken cancellationToken)
    {
        var names = await companies.GetAllAsync(cancellationToken);
        if (!ai.IsConfigured)
        {
            return Sanitize(null, query, names);
        }

        try
        {
            using var client = ai.CreateClient(useFunctionInvocation: false);
            var response = await client.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, SystemPrompt),
                    new ChatMessage(ChatRole.User, query),
                ],
                new ChatOptions { MaxOutputTokens = 300, Temperature = 0f },
                cancellationToken);
            return Sanitize(Parse(response.Text ?? ""), query, names);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Plain-English job search failed. Using the words as a keyword.");
            return Sanitize(null, query, names);
        }
    }

    public static SearchFilter Sanitize(SearchDraft? draft, string query, IReadOnlyList<Company> companies)
    {
        var keyword = (draft?.Keyword ?? query).Trim();
        if (keyword.Length > 80)
        {
            keyword = keyword[..80];
        }

        if (keyword.Length == 0)
        {
            keyword = query.Trim();
        }

        string? state = null;
        if (!string.IsNullOrWhiteSpace(draft?.State) && draft.State.Trim().Length == 2 && draft.State.All(char.IsLetter))
        {
            state = draft.State.Trim().ToUpperInvariant();
        }

        double? pay = draft?.MinPayPerHour is >= 0 ? draft.MinPayPerHour : null;
        Company? company = null;
        if (!string.IsNullOrWhiteSpace(draft?.CompanyName))
        {
            company = companies.FirstOrDefault(item => item.Name.Equals(draft.CompanyName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return new SearchFilter(
            keyword,
            company?.CompanyId,
            company?.Name,
            state,
            draft?.IsRemote,
            draft?.IsPaid,
            pay,
            draft?.ExcludeCompaniesWithApprovedReports ?? false);
    }

    private static SearchDraft? Parse(string raw)
    {
        var json = raw.Trim();
        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json[start..(end + 1)]);
            var root = document.RootElement;
            return new SearchDraft(
                ReadString(root, "keyword"),
                ReadString(root, "companyName"),
                ReadString(root, "state"),
                ReadBool(root, "isRemote"),
                ReadBool(root, "isPaid"),
                ReadDouble(root, "minPayPerHour"),
                ReadBool(root, "excludeCompaniesWithApprovedReports") ?? false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? ReadBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static double? ReadDouble(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public sealed record SearchDraft(
        string? Keyword,
        string? CompanyName,
        string? State,
        bool? IsRemote,
        bool? IsPaid,
        double? MinPayPerHour,
        bool ExcludeCompaniesWithApprovedReports);
}
