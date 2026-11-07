using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobLensChat.Services;
using ModelContextProtocol.Server;

namespace JobLensChat.Mcp;

[McpServerToolType]
public sealed class DomainTools(RdapService rdap, ILogger<DomainTools> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    [McpServerTool(Name = "lookup_domain_registration")]
    [Description("Live external lookup (RDAP) of a domain's public registration record: registration date, domain age in days, registrar, expiration date, and status. Use this to judge how new a recruiter's domain is. This data comes from the public RDAP service, NOT from the JobLens scam-report database.")]
    public async Task<string> LookupDomainRegistration(
        [Description("Exact domain, e.g. amaz0n-careers.net or google.com (no http:// or email username).")] string domain,
        CancellationToken cancellationToken)
    {
        const string tool = "lookup_domain_registration";
        if (!TryNormalizeDomain(domain, out var host, out var error))
        {
            logger.LogInformation("MCP tool {Tool} domain {Domain} found {Found} cache {FromCache}", tool, domain, false, false);
            return error;
        }

        var result = await rdap.LookupAsync(host, cancellationToken);
        logger.LogInformation(
            "MCP tool {Tool} domain {Domain} found {Found} cache {FromCache}",
            tool,
            host,
            result.Found,
            result.FromCache);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private static bool TryNormalizeDomain(string? domain, out string host, out string error)
    {
        host = "";
        var text = domain?.Trim() ?? "";
        if (text.Length == 0)
        {
            error = "Domain is required.";
            return false;
        }

        var at = text.LastIndexOf('@');
        if (at >= 0)
        {
            text = at < text.Length - 1 ? text[(at + 1)..] : "";
        }

        host = text.Trim().ToLowerInvariant();
        if (host.Length == 0)
        {
            error = "Domain is required.";
            return false;
        }

        if (host.Length > 100)
        {
            error = "Domain must be 100 characters or fewer.";
            return false;
        }

        if (!Regex.IsMatch(host, "^[a-z0-9.-]+\\.[a-z]{2,}$"))
        {
            error = "Domain must be an exact hostname such as amaz0n-careers.net, without http://.";
            return false;
        }

        error = "";
        return true;
    }
}
