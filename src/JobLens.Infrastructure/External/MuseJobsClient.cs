using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobLens.Infrastructure.External;

public sealed partial class MuseJobsClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<MuseJobsClient> logger) : ILiveJobsProvider
{
    public const int CacheEntrySize = 1;
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(30);
    public const string BusyMessage = "Live job search is busy — try again in a few minutes";
    public const string UnavailableMessage = "Live job search is temporarily unavailable.";
    public const string UnsupportedFilterMessage = "That filter isn't supported by The Muse — try another category";

    public string Name => LiveJobsServiceNames.TheMuse;

    public string AttributionText =>
        "Job listings from The Muse. Live listings are fetched on demand and not stored by JobLens.";

    public string AttributionUrl => "https://www.themuse.com";

    public async Task<LiveJobsResult> SearchAsync(LiveJobsQuery query, CancellationToken cancellationToken)
    {
        var search = new LiveJobSearch(
            query.Category,
            query.Level ?? "Internship",
            query.Location,
            query.CompanyName,
            query.Keyword,
            query.Page);
        var legacy = await SearchLegacyAsync(search, cancellationToken);
        return legacy.ToProviderResult();
    }

    /// <summary>Internal Muse search used by unit tests.</summary>
    internal async Task<LiveJobResult> SearchAsync(LiveJobSearch search, CancellationToken cancellationToken) =>
        await SearchLegacyAsync(search, cancellationToken);

    private async Task<LiveJobResult> SearchLegacyAsync(LiveJobSearch search, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(search);
        if (cache.TryGetValue(cacheKey, out LiveJobResult? cached) && cached is not null)
        {
            return cached with { FromCache = true };
        }

        var url = BuildUrl(search);
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
            {
                var reason = $"HTTP {(int)response.StatusCode}";
                LogHttpError(response.StatusCode, url, body);
                if (cache.TryGetValue(cacheKey, out LiveJobResult? stale) && stale is not null)
                {
                    return stale with { FromCache = true, Message = BusyMessage, DebugDetail = DevDebug(reason) };
                }

                return Fail(search.Page, BusyMessage, reason);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                LogHttpError(response.StatusCode, url, body);
                return Fail(search.Page, UnsupportedFilterMessage, $"HTTP {(int)response.StatusCode}");
            }

            if (!response.IsSuccessStatusCode)
            {
                LogHttpError(response.StatusCode, url, body);
                return Fail(search.Page, UnavailableMessage, $"HTTP {(int)response.StatusCode}");
            }

            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var parsed = Parse(document.RootElement, ClampPage(search.Page));
            cache.Set(cacheKey, parsed, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheFor,
                Size = CacheEntrySize,
            });
            return parsed;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Muse error: {ExceptionType} {Message} after {ElapsedMs} ms",
                ex.GetType().Name,
                ex.Message,
                ElapsedMs(started));
            return Fail(search.Page, UnavailableMessage, $"{ex.GetType().Name}: {ex.Message}");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(
                "Muse error: {ExceptionType} {Message} after {ElapsedMs} ms",
                ex.GetType().Name,
                ex.Message,
                ElapsedMs(started));
            return Fail(search.Page, UnavailableMessage, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    internal string BuildUrl(LiveJobSearch search)
    {
        // The Muse jobs list uses 1-based pages; never send page=0.
        var apiPage = ClampPage(search.Page);
        var query = new StringBuilder("jobs?page=").Append(apiPage);
        AppendParam(query, "category", search.Category);
        AppendParam(query, "level", search.Level);
        AppendParam(query, "location", search.Location);
        AppendParam(query, "company", search.CompanyName);

        var apiKey = configuration["Jobs:TheMuse:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            query.Append("&api_key=").Append(Uri.EscapeDataString(apiKey.Trim()));
        }

        return query.ToString();
    }

    internal static bool IncludesApiKey(string relativeUrl) =>
        relativeUrl.Contains("api_key=", StringComparison.Ordinal);

    internal static string RedactApiKey(string url) =>
        ApiKeyPattern().Replace(url, "$1***");

    internal static int ClampPage(int page) => Math.Clamp(page <= 0 ? 1 : page, 1, 5);

    internal static LiveJobResult Parse(JsonElement root, int requestedPage)
    {
        var page = ClampPage(requestedPage);
        if (root.TryGetProperty("page", out var pageNode) && pageNode.TryGetInt32(out var apiPage))
        {
            // Muse may report 0 on older responses; keep the requested 1-based page in that case.
            page = apiPage < 1 ? ClampPage(requestedPage) : Math.Clamp(apiPage, 1, 5);
        }

        var pageCount = 1;
        if (root.TryGetProperty("page_count", out var countNode) && countNode.TryGetInt32(out var count))
        {
            pageCount = Math.Clamp(Math.Max(count, 1), 1, 5);
        }

        var items = new List<LiveJob>();
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in results.EnumerateArray())
            {
                var job = ReadJob(row);
                if (job is not null)
                {
                    items.Add(job);
                }
            }
        }

        return new LiveJobResult(items, page, pageCount, null, false);
    }

    internal static LiveJobResult ParseJson(string json, int requestedPage)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement, requestedPage);
    }

    private LiveJobResult Fail(int page, string message, string reason) =>
        new([], ClampPage(page), 1, message, false, DevDebug(reason));

    private string? DevDebug(string reason) =>
        environment.IsDevelopment() ? reason : null;

    private void LogHttpError(HttpStatusCode status, string url, string body)
    {
        var snippet = body.Length <= 300 ? body : body[..300];
        logger.LogWarning(
            "Muse error: HTTP {Status} url {Url} body {Body}",
            (int)status,
            RedactApiKey(url),
            snippet);
    }

    private static int ElapsedMs(long started) =>
        (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private static LiveJob? ReadJob(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = ReadScalar(row, "id") ?? "";
        var title = ReadString(row, "name") ?? "";
        if (id.Length == 0 || title.Length == 0)
        {
            return null;
        }

        var company = "";
        if (row.TryGetProperty("company", out var companyNode) && companyNode.ValueKind == JsonValueKind.Object)
        {
            company = ReadString(companyNode, "name") ?? "";
        }

        var applyUrl = "";
        if (row.TryGetProperty("refs", out var refs) && refs.ValueKind == JsonValueKind.Object)
        {
            applyUrl = ReadString(refs, "landing_page") ?? "";
        }

        var locations = JoinNames(row, "locations");
        var isRemote = locations.Contains("Remote", StringComparison.OrdinalIgnoreCase)
            || locations.Contains("Flexible", StringComparison.OrdinalIgnoreCase);

        return new LiveJob(
            id,
            title,
            company,
            locations,
            JoinNames(row, "levels"),
            JoinNames(row, "categories"),
            Snippet(ReadString(row, "contents")),
            ReadString(row, "publication_date"),
            applyUrl,
            Source: "The Muse",
            IsRemote: isRemote);
    }

    private static string JoinNames(JsonElement row, string property)
    {
        if (!row.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var parts = new List<string>();
        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = ReadString(entry, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                parts.Add(name);
            }
        }

        return string.Join(", ", parts);
    }

    private static string Snippet(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var text = HtmlTagPattern().Replace(html, " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= 300 ? text : text[..300];
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadScalar(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null,
        };
    }

    private static void AppendParam(StringBuilder query, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        query.Append('&').Append(name).Append('=').Append(Uri.EscapeDataString(value.Trim()));
    }

    private static string CacheKey(LiveJobSearch search) =>
        string.Join('|',
            "muse",
            ClampPage(search.Page).ToString(),
            search.Category ?? "",
            search.Level ?? "",
            search.Location ?? "",
            search.CompanyName ?? "",
            search.Keyword ?? "");

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"([?&]api_key=)[^&]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyPattern();
}

internal static class LiveJobsServiceNames
{
    public const string TheMuse = "The Muse";
    public const string UsaJobs = "USAJOBS";
}
