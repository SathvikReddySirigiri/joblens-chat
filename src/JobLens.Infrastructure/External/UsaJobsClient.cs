using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
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

public sealed partial class UsaJobsClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<UsaJobsClient> logger) : ILiveJobsProvider
{
    public const int CacheEntrySize = 1;
    public const int ResultsPerPage = 20;
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(30);
    public const string MissingCredentialsMessage =
        "Live jobs need a USAJOBS key — see README setup.";
    public const string BusyMessage = "Live job search is busy — try again in a few minutes";
    public const string UnavailableMessage = "Live job search is temporarily unavailable.";

    public string Name => LiveJobsServiceNames.UsaJobs;

    public string AttributionText =>
        "Job listings from USAJOBS.gov (official U.S. government source). Fetched on demand, not stored by JobLens.";

    public string AttributionUrl => "https://www.usajobs.gov";

    public async Task<LiveJobsResult> SearchAsync(LiveJobsQuery query, CancellationToken cancellationToken)
    {
        var apiKey = configuration["Jobs:UsaJobs:ApiKey"]?.Trim() ?? "";
        var userAgent = configuration["Jobs:UsaJobs:UserAgent"]?.Trim() ?? "";
        if (apiKey.Length == 0 || userAgent.Length == 0)
        {
            return Fail(query.Page, MissingCredentialsMessage, "missing UsaJobs ApiKey or UserAgent");
        }

        var cacheKey = CacheKey(query);
        if (cache.TryGetValue(cacheKey, out LiveJobsResult? cached) && cached is not null)
        {
            return cached with { FromCache = true };
        }

        var url = BuildUrl(query);
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Host = "data.usajobs.gov";
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            request.Headers.TryAddWithoutValidation("Authorization-Key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
            {
                LogHttpError(response.StatusCode, url, body);
                if (cache.TryGetValue(cacheKey, out LiveJobsResult? stale) && stale is not null)
                {
                    return stale with { FromCache = true };
                }

                return Fail(query.Page, BusyMessage, $"HTTP {(int)response.StatusCode}");
            }

            if (!response.IsSuccessStatusCode)
            {
                LogHttpError(response.StatusCode, url, body);
                return Fail(query.Page, UnavailableMessage, $"HTTP {(int)response.StatusCode}");
            }

            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var parsed = Parse(document.RootElement, ClampPage(query.Page));
            cache.Set(cacheKey, parsed, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheFor,
                Size = CacheEntrySize,
            });
            return parsed;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogException(ex, started);
            return Fail(query.Page, UnavailableMessage, $"{ex.GetType().Name}: {ex.Message}");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            LogException(ex, started);
            return Fail(query.Page, UnavailableMessage, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    internal string BuildUrl(LiveJobsQuery query)
    {
        var page = ClampPage(query.Page);
        var builder = new StringBuilder("search?ResultsPerPage=")
            .Append(ResultsPerPage)
            .Append("&Page=")
            .Append(page)
            .Append("&SortField=OpenDate")
            .Append("&SortDirection=Desc");

        AppendParam(builder, "Keyword", query.Keyword);
        AppendParam(builder, "LocationName", query.Location);
        AppendParam(builder, "JobCategoryCode", query.CategoryCode);
        AppendParam(builder, "HiringPath", query.HiringPath);
        if (query.RemoteOnly)
        {
            AppendParam(builder, "RemoteIndicator", "True");
        }

        return builder.ToString();
    }

    internal static HttpRequestMessage CreateRequest(string relativeUrl, string userAgent, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.Host = "data.usajobs.gov";
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        request.Headers.TryAddWithoutValidation("Authorization-Key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    internal static int ClampPage(int page) => Math.Clamp(page <= 0 ? 1 : page, 1, 5);

    internal static LiveJobsResult ParseJson(string json, int requestedPage)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement, requestedPage);
    }

    internal static LiveJobsResult Parse(JsonElement root, int requestedPage)
    {
        var page = ClampPage(requestedPage);
        var searchResult = FindProperty(root, "SearchResult") ?? root;
        var pageCount = 1;
        if (TryGetInt(searchResult, "SearchResultCountAll", out var total)
            || TryGetInt(root, "SearchResultCountAll", out total))
        {
            pageCount = Math.Clamp(Math.Max((int)Math.Ceiling(total / (double)ResultsPerPage), 1), 1, 5);
        }

        var items = new List<LiveJob>();
        var array = FindProperty(searchResult, "SearchResultItems")
            ?? FindProperty(root, "SearchResultItems");
        if (array is JsonElement itemsNode && itemsNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in itemsNode.EnumerateArray())
            {
                try
                {
                    var job = ReadJob(row);
                    if (job is not null)
                    {
                        items.Add(job);
                    }
                }
                catch (Exception)
                {
                    // Skip malformed items rather than failing the whole page.
                }
            }
        }

        return new LiveJobsResult(items, page, pageCount, null, false);
    }

    private static LiveJob? ReadJob(JsonElement row)
    {
        var descriptor = FindProperty(row, "MatchedObjectDescriptor") ?? row;
        var id = ReadString(row, "MatchedObjectId")
            ?? ReadString(descriptor, "PositionID")
            ?? "";
        var title = ReadString(descriptor, "PositionTitle") ?? "";
        if (id.Length == 0 || title.Length == 0)
        {
            return null;
        }

        var organization = ReadString(descriptor, "OrganizationName") ?? "";
        var department = ReadString(descriptor, "DepartmentName") ?? "";
        var company = string.IsNullOrWhiteSpace(organization)
            ? department
            : string.IsNullOrWhiteSpace(department) ? organization : $"{organization} ({department})";

        var location = ReadString(descriptor, "PositionLocationDisplay") ?? "";
        var category = "";
        var categories = FindProperty(descriptor, "JobCategory");
        if (categories is JsonElement categoryArray
            && categoryArray.ValueKind == JsonValueKind.Array
            && categoryArray.GetArrayLength() > 0)
        {
            category = ReadString(categoryArray[0], "Name") ?? "";
        }

        var applyUrl = "";
        var apply = FindProperty(descriptor, "ApplyURI");
        if (apply is JsonElement applyArray
            && applyArray.ValueKind == JsonValueKind.Array
            && applyArray.GetArrayLength() > 0
            && applyArray[0].ValueKind == JsonValueKind.String)
        {
            applyUrl = applyArray[0].GetString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(applyUrl))
        {
            applyUrl = ReadString(descriptor, "PositionURI") ?? "";
        }

        var payRange = "";
        var pay = FindProperty(descriptor, "PositionRemuneration");
        if (pay is JsonElement payArray
            && payArray.ValueKind == JsonValueKind.Array
            && payArray.GetArrayLength() > 0)
        {
            payRange = FormatPay(payArray[0]);
        }

        var userArea = FindProperty(descriptor, "UserArea");
        var details = userArea is JsonElement area
            ? FindProperty(area, "Details")
            : null;
        var summary = details is JsonElement detailSummary ? ReadString(detailSummary, "JobSummary") : null;
        var lowGrade = details is JsonElement detailLow ? ReadString(detailLow, "LowGrade") : null;
        var highGrade = details is JsonElement detailHigh ? ReadString(detailHigh, "HighGrade") : null;
        var remote = details is JsonElement detailRemote
            && IsTruthy(FindProperty(detailRemote, "RemoteIndicator"));

        var grade = FormatGrade(lowGrade, highGrade);
        var published = ReadString(descriptor, "PublicationStartDate");
        var close = ReadString(descriptor, "ApplicationCloseDate");

        return new LiveJob(
            id,
            title,
            company,
            location,
            grade,
            category,
            Snippet(summary),
            published,
            applyUrl,
            Source: LiveJobsServiceNames.UsaJobs,
            CloseDate: close,
            PayRange: NullIfEmpty(payRange),
            IsRemote: remote);
    }

    private static string FormatPay(JsonElement pay)
    {
        var min = ReadString(pay, "MinimumRange");
        var max = ReadString(pay, "MaximumRange");
        var interval = ReadString(pay, "RateIntervalCode") ?? "";
        if (string.IsNullOrWhiteSpace(min) && string.IsNullOrWhiteSpace(max))
        {
            return "";
        }

        var unit = interval.Contains("Hour", StringComparison.OrdinalIgnoreCase) ? "hour"
            : interval.Contains("Week", StringComparison.OrdinalIgnoreCase) ? "week"
            : interval.Contains("Month", StringComparison.OrdinalIgnoreCase) ? "month"
            : "year";

        string Money(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)
                ? amount.ToString("$#,##0.##", CultureInfo.InvariantCulture)
                : value;
        }

        var left = Money(min);
        var right = Money(max);
        if (left.Length > 0 && right.Length > 0 && left != right)
        {
            return $"{left}–{right} per {unit}";
        }

        var single = left.Length > 0 ? left : right;
        return $"{single} per {unit}";
    }

    private static string FormatGrade(string? low, string? high)
    {
        low = low?.Trim() ?? "";
        high = high?.Trim() ?? "";
        if (low.Length == 0 && high.Length == 0)
        {
            return "";
        }

        if (low.Length > 0 && high.Length > 0 && !low.Equals(high, StringComparison.OrdinalIgnoreCase))
        {
            return $"GS-{low}–{high}";
        }

        return $"GS-{(low.Length > 0 ? low : high)}";
    }

    private LiveJobsResult Fail(int page, string message, string reason) =>
        new([], ClampPage(page), 1, message, false, DevDebug(reason));

    private string? DevDebug(string reason) =>
        environment.IsDevelopment() ? reason : null;

    private void LogException(Exception ex, long started) =>
        logger.LogWarning(
            "UsaJobs error: {ExceptionType} {Message} after {ElapsedMs} ms",
            ex.GetType().Name,
            ex.Message,
            (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    private void LogHttpError(HttpStatusCode status, string url, string body)
    {
        var snippet = body.Length <= 300 ? body : body[..300];
        logger.LogWarning(
            "UsaJobs error: HTTP {Status} url {Url} body {Body}",
            (int)status,
            url,
            snippet);
    }

    private static string CacheKey(LiveJobsQuery query) =>
        string.Join('|',
            "usajobs",
            ClampPage(query.Page).ToString(),
            query.Keyword ?? "",
            query.Location ?? "",
            query.CategoryCode ?? "",
            query.HiringPath ?? "",
            query.RemoteOnly ? "1" : "0");

    private static void AppendParam(StringBuilder query, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        query.Append('&').Append(name).Append('=').Append(Uri.EscapeDataString(value.Trim()));
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

    private static JsonElement? FindProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static bool TryGetInt(JsonElement element, string name, out int value)
    {
        value = 0;
        var node = FindProperty(element, name);
        if (node is null)
        {
            return false;
        }

        if (node.Value.ValueKind == JsonValueKind.Number && node.Value.TryGetInt32(out value))
        {
            return true;
        }

        if (node.Value.ValueKind == JsonValueKind.String
            && int.TryParse(node.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return false;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        var node = FindProperty(element, name);
        if (node is null)
        {
            return null;
        }

        return node.Value.ValueKind switch
        {
            JsonValueKind.String => node.Value.GetString(),
            JsonValueKind.Number => node.Value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static bool IsTruthy(JsonElement? node)
    {
        if (node is null)
        {
            return false;
        }

        return node.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => bool.TryParse(node.Value.GetString(), out var flag) && flag
                || string.Equals(node.Value.GetString(), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Value.GetString(), "Y", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => node.Value.TryGetInt32(out var n) && n != 0,
            _ => false,
        };
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagPattern();
}

internal static class LiveJobsServiceNames
{
    public const string UsaJobs = "USAJOBS";
}
