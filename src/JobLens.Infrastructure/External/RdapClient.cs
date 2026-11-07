using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using JobLens.Application.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace JobLens.Infrastructure.External;

// Live public RDAP lookup. Results stay in memory for six hours and are never written to SQLite.
public sealed class RdapClient(HttpClient httpClient, IMemoryCache cache, ILogger<RdapClient> logger) : JobLens.Application.Interfaces.IRdapClient
{
    public const string SourceLabel = "RDAP (live external lookup, not the JobLens database)";
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);

    public async Task<RdapResult> LookupAsync(string domain, CancellationToken cancellationToken)
    {
        var key = "rdap:" + domain;
        if (cache.TryGetValue(key, out RdapResult? cached) && cached is not null)
        {
            return cached with { FromCache = true };
        }

        foreach (var url in RegistryUrls(domain))
        {
            var fetched = await FetchAsync(domain, url, cancellationToken);
            if (fetched is null)
            {
                continue;
            }

            // Success and 404 are stable answers. Timeouts and 5xx are not cached.
            cache.Set(key, fetched, CacheFor);
            return fetched;
        }

        return Unavailable(domain);
    }

    internal static IReadOnlyList<string> RegistryUrls(string domain)
    {
        var host = domain.Trim().ToLowerInvariant();
        var escaped = Uri.EscapeDataString(host);
        var fallback = "https://rdap.org/domain/" + escaped;
        var dot = host.LastIndexOf('.');
        var tld = dot >= 0 ? host[(dot + 1)..] : "";
        var primary = tld switch
        {
            "com" => "https://rdap.verisign.com/com/v1/domain/" + escaped,
            "net" => "https://rdap.verisign.com/net/v1/domain/" + escaped,
            "org" => "https://rdap.publicinterestregistry.org/rdap/domain/" + escaped,
            _ => null,
        };

        return primary is null ? [fallback] : [primary, fallback];
    }

    private async Task<RdapResult?> FetchAsync(string domain, string url, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        // Own 6s budget. The caller's token is linked only so a disconnect still cancels the attempt.
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(TimeSpan.FromSeconds(6));
        try
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, attempt.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                LogOk(url, started);
                return NotFound(domain);
            }

            if (!response.IsSuccessStatusCode)
            {
                LogFailure(url, started, "HttpRequestException", $"HTTP {(int)response.StatusCode}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(attempt.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: attempt.Token);
            LogOk(url, started);
            return Parse(domain, document.RootElement);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailure(url, started, ex.GetType().Name, ex.Message);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            LogFailure(url, started, ex.GetType().Name, ex.Message);
            return null;
        }
    }

    private void LogOk(string url, long started)
    {
        logger.LogInformation(
            "RDAP ok via {host} in {ms} ms",
            new Uri(url).Host,
            ElapsedMs(started));
    }

    private void LogFailure(string url, long started, string exceptionType, string? message)
    {
        logger.LogWarning(
            "RDAP error: {url} {exceptionType} {message} after {ms} ms",
            url,
            exceptionType,
            message,
            ElapsedMs(started));
    }

    private static int ElapsedMs(long started) =>
        (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private static RdapResult Parse(string domain, JsonElement root)
    {
        var registration = EventDate(root, "registration");
        var expiration = EventDate(root, "expiration");
        // Exact action match, so "last update of RDAP database" is not treated as "last changed".
        var changed = EventDate(root, "last changed");

        return new RdapResult(
            domain,
            true,
            registration,
            expiration,
            changed,
            Registrar(root),
            AgeDays(registration),
            ReadStatus(root),
            ReadNameservers(root),
            "RDAP registration record found.",
            SourceLabel,
            DateTimeOffset.UtcNow);
    }

    private static RdapResult NotFound(string domain) => new(
        domain,
        false,
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        "No RDAP record found — the domain may not be registered.",
        SourceLabel,
        DateTimeOffset.UtcNow);

    private static RdapResult Unavailable(string domain) => new(
        domain,
        false,
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        "Live domain lookup is temporarily unavailable.",
        SourceLabel,
        DateTimeOffset.UtcNow);

    private static string? EventDate(JsonElement root, string action)
    {
        if (!root.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entry in events.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("eventAction", out var eventAction)
                || eventAction.ValueKind != JsonValueKind.String
                || !string.Equals(eventAction.GetString(), action, StringComparison.OrdinalIgnoreCase)
                || !entry.TryGetProperty("eventDate", out var eventDate)
                || eventDate.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            return CalendarDate(eventDate.GetString());
        }

        return null;
    }

    private static string? CalendarDate(string? eventDate)
    {
        if (!DateTimeOffset.TryParse(eventDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant))
        {
            return null;
        }

        return DateOnly.FromDateTime(instant.UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string? Registrar(JsonElement root)
    {
        if (!root.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entity in entities.EnumerateArray())
        {
            if (entity.ValueKind != JsonValueKind.Object || !HasRole(entity, "registrar"))
            {
                continue;
            }

            // Only this entity's own vcard. A nested abuse contact with an empty fn must not replace the registrar.
            var name = VcardFn(entity);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }

        return null;
    }

    private static bool HasRole(JsonElement entity, string role)
    {
        if (!entity.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var entry in roles.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String
                && string.Equals(entry.GetString(), role, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? VcardFn(JsonElement entity)
    {
        if (!entity.TryGetProperty("vcardArray", out var vcard)
            || vcard.ValueKind != JsonValueKind.Array
            || vcard.GetArrayLength() < 2
            || vcard[1].ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var property in vcard[1].EnumerateArray())
        {
            if (property.ValueKind != JsonValueKind.Array
                || property.GetArrayLength() < 4
                || property[0].ValueKind != JsonValueKind.String
                || !string.Equals(property[0].GetString(), "fn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property[3].ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = property[3].GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadStatus(JsonElement root)
    {
        var values = new List<string>();
        if (!root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (var entry in status.EnumerateArray())
        {
            if (values.Count == 10)
            {
                break;
            }

            if (entry.ValueKind == JsonValueKind.String && entry.GetString() is { Length: > 0 } value)
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<string> ReadNameservers(JsonElement root)
    {
        var values = new List<string>();
        if (!root.TryGetProperty("nameservers", out var servers) || servers.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (var server in servers.EnumerateArray())
        {
            if (values.Count == 5)
            {
                break;
            }

            if (server.ValueKind == JsonValueKind.Object
                && server.TryGetProperty("ldhName", out var name)
                && name.ValueKind == JsonValueKind.String
                && name.GetString() is { Length: > 0 } value)
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static int? AgeDays(string? registrationDate)
    {
        if (!DateOnly.TryParseExact(registrationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var registered))
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return today.DayNumber - registered.DayNumber;
    }

    internal static RdapResult ParseForTests(string domain, string json)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(domain, document.RootElement);
    }
}
