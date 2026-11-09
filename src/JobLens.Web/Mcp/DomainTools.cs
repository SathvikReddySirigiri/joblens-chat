using System.ComponentModel;
using System.Text.Json;
using JobLens.Application.Common;
using JobLens.Application.Interfaces;
using ModelContextProtocol.Server;

namespace JobLens.Web.Mcp;

[McpServerToolType]
public sealed class DomainTools(IRdapClient rdap, ILogger<DomainTools> logger)
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
        var timer = McpToolLog.Start();
        if (!DomainNormalizer.TryNormalizeDomain(domain, out var host, out var error))
        {
            McpToolLog.Information(logger, tool, new { domain }, timer);
            return error;
        }

        var result = await rdap.LookupAsync(host, cancellationToken);
        McpToolLog.Information(logger, tool, new { domain = host }, timer);
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
