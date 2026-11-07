using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using JobLensChat.Models;
using Microsoft.Extensions.Caching.Memory;

namespace JobLensChat.Services;

// Live SEC company-ticker file. Cached in memory for a day and never written to SQLite.
public sealed class EdgarService(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration, ILogger<EdgarService> logger)
{
    public const string SourceLabel = "SEC EDGAR (live public registry, not the JobLens database)";
    public const string NotFoundMessage = "Not found in SEC public-company registry — this does NOT mean the company is fake (private companies are not listed).";
    public const string UnavailableMessage = "SEC registry lookup temporarily unavailable.";
    private const string TickersUrl = "https://www.sec.gov/files/company_tickers.json";
    private const string CacheKey = "edgar:tickers";
    private bool _warned;

    public async Task<EdgarResult> MatchCompany(string name, CancellationToken cancellationToken)
    {
        try
        {
            var companies = await LoadAsync(cancellationToken);
            var match = BestMatch(name, companies);
            if (match is null)
            {
                return new EdgarResult(false, null, null, null, NotFoundMessage, SourceLabel);
            }

            return new EdgarResult(true, match.Title, match.Cik, match.Ticker, "Matched in the SEC public-company registry.", SourceLabel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EDGAR error: {Message}", ex.Message);
            return new EdgarResult(false, null, null, null, UnavailableMessage, SourceLabel);
        }
    }

    internal static string NormalizeName(string name)
    {
        var words = Tokens(name).Where(word => word is not ("inc" or "corp" or "co" or "llc" or "ltd" or "the"));
        return string.Concat(words);
    }

    internal static EdgarCompany? BestMatch(string name, IReadOnlyList<EdgarCompany> companies)
    {
        var query = NormalizeName(name);
        if (query.Length == 0)
        {
            return null;
        }

        EdgarCompany? exact = null;
        EdgarCompany? fuzzy = null;
        var fuzzyDistance = int.MaxValue;

        foreach (var company in companies)
        {
            var full = NormalizeName(company.Title);
            var first = Tokens(company.Title).FirstOrDefault(word => word is not ("inc" or "corp" or "co" or "llc" or "ltd" or "the")) ?? "";
            var distance = Math.Min(Levenshtein(query, full), first.Length == 0 ? int.MaxValue : Levenshtein(query, first));
            if (distance == 0)
            {
                exact ??= company;
                continue;
            }

            if (distance <= 2 && distance < fuzzyDistance)
            {
                fuzzy = company;
                fuzzyDistance = distance;
            }
        }

        return exact ?? fuzzy;
    }

    private async Task<IReadOnlyList<EdgarCompany>> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<EdgarCompany>? cached) && cached is not null)
        {
            return cached;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, TickersUrl);
        request.Headers.UserAgent.ParseAdd(UserAgent());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var list = new List<EdgarCompany>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var item = property.Value;
            var title = item.GetProperty("title").GetString() ?? "";
            var ticker = item.GetProperty("ticker").GetString() ?? "";
            var cikNumber = item.GetProperty("cik_str").GetInt64();
            list.Add(new EdgarCompany(title, cikNumber.ToString("D10", CultureInfo.InvariantCulture), ticker));
        }

        cache.Set(CacheKey, list, TimeSpan.FromHours(24));
        return list;
    }

    private string UserAgent()
    {
        var configured = configuration["Edgar:UserAgent"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        if (!_warned)
        {
            _warned = true;
            logger.LogWarning("EDGAR User-Agent is empty. Using a class-project default that the SEC may reject.");
        }

        return "JobLensChat/1.0 (ISM6225 class project)";
    }

    private static IEnumerable<string> Tokens(string name)
    {
        var buffer = new List<char>();
        foreach (var character in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Add(character);
            }
            else if (buffer.Count > 0)
            {
                yield return new string(buffer.ToArray());
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            yield return new string(buffer.ToArray());
        }
    }

    private static int Levenshtein(string source, string target)
    {
        if (source.Length == 0)
        {
            return target.Length;
        }

        if (target.Length == 0)
        {
            return source.Length;
        }

        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];
        for (var column = 0; column <= target.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= source.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= target.Length; column++)
            {
                var substitution = source[row - 1] == target[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }
}

public sealed record EdgarCompany(string Title, string Cik, string Ticker);
