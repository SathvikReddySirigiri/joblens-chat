using Microsoft.Extensions.Logging;
using JobLens.Application.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;
using JobLens.Application.Dtos;
using Microsoft.Extensions.AI;

namespace JobLens.Application.Services;

public sealed partial class SmartSearchService(IAiClientFactory ai, ICompanyRepository companies, ILogger<SmartSearchService> logger)
{
    private const string SystemPrompt =
        """
        Convert a plain-English job search into JSON only. Do not write SQL.
        Shape: {"keyword":"","companyName":"","state":"","isRemote":null,"isPaid":null,"minPayPerHour":null,"excludeCompaniesWithApprovedReports":false}
        Use null when the user did not ask for that filter. state is a 2-letter US code or null.
        keyword is the job words that are not a company, state, or pay filter. Keep keyword under 80 characters.
        """;

    private static readonly Dictionary<string, string> StateNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alabama"] = "AL", ["alaska"] = "AK", ["arizona"] = "AZ", ["arkansas"] = "AR",
        ["california"] = "CA", ["colorado"] = "CO", ["connecticut"] = "CT", ["delaware"] = "DE",
        ["florida"] = "FL", ["georgia"] = "GA", ["hawaii"] = "HI", ["idaho"] = "ID",
        ["illinois"] = "IL", ["indiana"] = "IN", ["iowa"] = "IA", ["kansas"] = "KS",
        ["kentucky"] = "KY", ["louisiana"] = "LA", ["maine"] = "ME", ["maryland"] = "MD",
        ["massachusetts"] = "MA", ["michigan"] = "MI", ["minnesota"] = "MN", ["mississippi"] = "MS",
        ["missouri"] = "MO", ["montana"] = "MT", ["nebraska"] = "NE", ["nevada"] = "NV",
        ["new hampshire"] = "NH", ["new jersey"] = "NJ", ["new mexico"] = "NM", ["new york"] = "NY",
        ["north carolina"] = "NC", ["north dakota"] = "ND", ["ohio"] = "OH", ["oklahoma"] = "OK",
        ["oregon"] = "OR", ["pennsylvania"] = "PA", ["rhode island"] = "RI", ["south carolina"] = "SC",
        ["south dakota"] = "SD", ["tennessee"] = "TN", ["texas"] = "TX", ["utah"] = "UT",
        ["vermont"] = "VT", ["virginia"] = "VA", ["washington"] = "WA", ["west virginia"] = "WV",
        ["wisconsin"] = "WI", ["wyoming"] = "WY", ["district of columbia"] = "DC",
    };

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
            logger.LogWarning(ex, "Plain-English job search failed. Using the rule-based parser.");
            return Sanitize(null, query, names);
        }
    }

    public static SearchFilter Sanitize(SearchDraft? draft, string query, IReadOnlyList<Company> companies)
    {
        if (draft is null)
        {
            return ParseByRules(query, companies);
        }

        // Keep an empty model keyword empty — do not refill from the raw query.
        var keyword = (draft.Keyword ?? "").Trim();
        if (keyword.Length > 80)
        {
            keyword = keyword[..80];
        }

        string? state = null;
        if (!string.IsNullOrWhiteSpace(draft.State) && draft.State.Trim().Length == 2 && draft.State.All(char.IsLetter))
        {
            state = draft.State.Trim().ToUpperInvariant();
        }

        double? pay = draft.MinPayPerHour is >= 0 ? draft.MinPayPerHour : null;
        Company? company = null;
        if (!string.IsNullOrWhiteSpace(draft.CompanyName))
        {
            company = companies.FirstOrDefault(item => item.Name.Equals(draft.CompanyName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return new SearchFilter(
            keyword,
            company?.CompanyId,
            company?.Name,
            state,
            draft.IsRemote,
            draft.IsPaid,
            pay,
            draft.ExcludeCompaniesWithApprovedReports);
    }

    internal static SearchFilter ParseByRules(string query, IReadOnlyList<Company> companies)
    {
        var remaining = " " + (query ?? "").Trim() + " ";
        bool? isRemote = null;
        bool? isPaid = null;
        double? minPay = null;
        var exclude = false;
        string? state = null;
        Company? company = null;

        if (ContainsWord(remaining, "remote"))
        {
            isRemote = true;
            remaining = RemoveWord(remaining, "remote");
        }
        else if (ContainsWord(remaining, "on-site") || ContainsWord(remaining, "onsite") || ContainsWord(remaining, "on site"))
        {
            isRemote = false;
            remaining = RemoveWord(remaining, "on-site");
            remaining = RemoveWord(remaining, "onsite");
            remaining = RemovePhrase(remaining, "on site");
        }

        if (ContainsWord(remaining, "unpaid"))
        {
            isPaid = false;
            remaining = RemoveWord(remaining, "unpaid");
        }
        else if (ContainsWord(remaining, "paid"))
        {
            isPaid = true;
            remaining = RemoveWord(remaining, "paid");
        }

        if (NoScamReportsPattern().IsMatch(remaining))
        {
            exclude = true;
            remaining = NoScamReportsPattern().Replace(remaining, " ");
        }

        var payMatch = PayPattern().Match(remaining);
        if (payMatch.Success
            && double.TryParse(payMatch.Groups["amount"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var pay))
        {
            minPay = pay;
            remaining = remaining.Remove(payMatch.Index, payMatch.Length).Insert(payMatch.Index, " ");
        }

        foreach (var (name, code) in StateNames.OrderByDescending(pair => pair.Key.Length))
        {
            if (!ContainsPhrase(remaining, name))
            {
                continue;
            }

            state = code;
            remaining = RemovePhrase(remaining, name);
            break;
        }

        if (state is null)
        {
            foreach (Match stateCode in StateCodePattern().Matches(remaining))
            {
                var code = stateCode.Groups["code"].Value.ToUpperInvariant();
                if (!StateNames.ContainsValue(code))
                {
                    continue;
                }

                state = code;
                remaining = remaining.Remove(stateCode.Index, stateCode.Length).Insert(stateCode.Index, " ");
                break;
            }
        }

        foreach (var candidate in companies.OrderByDescending(item => item.Name.Length))
        {
            if (!ContainsPhrase(remaining, candidate.Name))
            {
                continue;
            }

            company = candidate;
            remaining = RemovePhrase(remaining, candidate.Name);
            break;
        }

        remaining = FillerPattern().Replace(remaining, " ");
        var keyword = Regex.Replace(remaining, @"\s+", " ").Trim();
        if (keyword.Length > 80)
        {
            keyword = keyword[..80];
        }

        return new SearchFilter(
            keyword,
            company?.CompanyId,
            company?.Name,
            state,
            isRemote,
            isPaid,
            minPay,
            exclude);
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

    private static bool ContainsWord(string text, string word) =>
        Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool ContainsPhrase(string text, string phrase) =>
        Regex.IsMatch(text, $@"\b{Regex.Escape(phrase)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string RemoveWord(string text, string word) =>
        Regex.Replace(text, $@"\b{Regex.Escape(word)}\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string RemovePhrase(string text, string phrase) =>
        Regex.Replace(text, $@"\b{Regex.Escape(phrase)}\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\b(?:no\s+scam\s+reports|without\s+scam\s+reports|exclude\s+scam\s+reports)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoScamReportsPattern();

    [GeneratedRegex(@"\$\s*(?<amount>\d+(?:\.\d+)?)\b|(?:\bover\s+|more\s+than\s+)(?<amount>\d+(?:\.\d+)?)\s+(?:an\s+hour|per\s+hour|/?\s*h(?:ou)?r)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PayPattern();

    [GeneratedRegex(@"\bin\s+(?<code>[A-Za-z]{2})\b|(?<code>\b[A-Z]{2}\b)", RegexOptions.CultureInvariant)]
    private static partial Regex StateCodePattern();

    [GeneratedRegex(@"\b(?:in|for|with|the|a|an|jobs?|job|internships?|internship|roles?|role|positions?|position)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FillerPattern();

    public sealed record SearchDraft(
        string? Keyword,
        string? CompanyName,
        string? State,
        bool? IsRemote,
        bool? IsPaid,
        double? MinPayPerHour,
        bool ExcludeCompaniesWithApprovedReports);
}
