using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using JobLens.Application.Common;
using JobLens.Application.Interfaces;
using JobLens.Application.Services;
using JobLens.Domain.Constants;
using ModelContextProtocol.Server;

namespace JobLens.Web.Mcp;

[McpServerToolType]
public sealed class ScamTools(IScamAnalyticsRepository repository, RecruiterRiskService riskService, ILogger<ScamTools> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly string[] ScamTypes =
    [
        "Fake check",
        "Equipment purchase",
        "Upfront fee",
        "Chat-only interview",
        "Identity theft",
        "Fake staffing agency",
    ];

    private static readonly string[] Channels =
    [
        "Email",
        "Telegram",
        "WhatsApp",
        "LinkedIn",
        "Text message",
    ];

    private static readonly string[] Statuses = ["Pending", "Approved", "Rejected"];

    [McpServerTool(Name = "get_domain_report")]
    [Description("Exact lookup: return approved scam report details for one exact recruiter email domain (for example amaz0n-careers.net), including the company being impersonated. Returns at most 20 approved reports.")]
    public async Task<string> GetDomainReport(
        [Description("Exact domain only, without http:// or an email username, e.g. amaz0n-careers.net.")] string domain,
        CancellationToken cancellationToken)
    {
        const string tool = "get_domain_report";
        var timer = McpToolLog.Start();
        if (!DomainNormalizer.TryNormalizeDomain(domain, out var host, out var error))
        {
            McpToolLog.Information(logger, tool, new { domain }, timer);
            return error;
        }

        var reports = (await repository.GetReportsByDomainAsync(host, cancellationToken))
            .Where(report => string.Equals(report.Status, ReportStatus.Approved, StringComparison.Ordinal))
            .Take(20)
            .ToList();
        McpToolLog.Information(logger, tool, new { domain = host }, timer);

        if (reports.Count == 0)
        {
            return Json(new
            {
                domain = host,
                reportCount = 0,
                message = "No approved scam reports found for this domain.",
            });
        }

        return Json(new
        {
            domain = host,
            reportCount = reports.Count,
            reports,
        });
    }

    [McpServerTool(Name = "search_scam_reports")]
    [Description("Filtered search of approved scam report details by scam type, impersonated company, channel, and date range. All filters are optional. Returns at most 20 approved reports, newest first. Use summarize_scams for counts by status.")]
    public async Task<string> SearchScamReports(
        [Description("Optional scam type. One of: Fake check, Equipment purchase, Upfront fee, Chat-only interview, Identity theft, Fake staffing agency.")] string? scamType = null,
        [Description("Optional exact company name, e.g. Amazon, Deloitte.")] string? companyName = null,
        [Description("Optional channel. One of: Email, Telegram, WhatsApp, LinkedIn, Text message.")] string? channel = null,
        [Description("Optional status. Only Approved report details are returned; Pending or Rejected yields no detail rows.")] string? status = null,
        [Description("Optional inclusive start date, exactly yyyy-MM-dd.")] string? fromDate = null,
        [Description("Optional inclusive end date, exactly yyyy-MM-dd.")] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        const string tool = "search_scam_reports";
        var timer = McpToolLog.Start();
        if (!TryChoice(scamType, ScamTypes, "scamType", out var scam, out var error)
            || !TryChoice(channel, Channels, "channel", out var channelValue, out error)
            || !TryChoice(status, Statuses, "status", out var statusValue, out error)
            || !TryDateRange(fromDate, toDate, out var from, out var to, out error))
        {
            McpToolLog.Information(logger, tool, new { scamType, companyName, channel, status, fromDate, toDate }, timer);
            return error!;
        }

        var company = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        if (statusValue is not null
            && !string.Equals(statusValue, ReportStatus.Approved, StringComparison.Ordinal))
        {
            McpToolLog.Information(logger, tool, new { scamType = scam, companyName = company, channel = channelValue, status = statusValue, fromDate = from, toDate = to }, timer);
            return Json(new
            {
                filters = new { scamType = scam, companyName = company, channel = channelValue, status = statusValue, fromDate = from, toDate = to },
                resultCount = 0,
                reports = Array.Empty<object>(),
                message = "Only approved report details are available through this tool. Use summarize_scams for counts by status.",
            });
        }

        var reports = await repository.SearchReportsAsync(
            scam,
            company,
            channelValue,
            ReportStatus.Approved,
            from,
            to,
            cancellationToken);
        McpToolLog.Information(logger, tool, new { scamType = scam, companyName = company, channel = channelValue, status = ReportStatus.Approved, fromDate = from, toDate = to }, timer);

        if (reports.Count == 0)
        {
            return Json(new
            {
                filters = new { scamType = scam, companyName = company, channel = channelValue, status = ReportStatus.Approved, fromDate = from, toDate = to },
                resultCount = 0,
                reports,
                message = "No approved scam reports matched these filters.",
            });
        }

        return Json(new
        {
            filters = new { scamType = scam, companyName = company, channel = channelValue, status = ReportStatus.Approved, fromDate = from, toDate = to },
            resultCount = reports.Count,
            reports,
        });
    }

    [McpServerTool(Name = "summarize_scams")]
    [Description("Aggregate summary of scam reports for an optional date range: total reports, counts by status, counts by scam type and channel, and total and average money requested by scammers in USD.")]
    public async Task<string> SummarizeScams(
        [Description("Optional inclusive start date, exactly yyyy-MM-dd.")] string? fromDate = null,
        [Description("Optional inclusive end date, exactly yyyy-MM-dd.")] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        const string tool = "summarize_scams";
        var timer = McpToolLog.Start();
        if (!TryDateRange(fromDate, toDate, out var from, out var to, out var error))
        {
            McpToolLog.Information(logger, tool, new { fromDate, toDate }, timer);
            return error!;
        }

        var summary = await repository.SummarizeAsync(from, to, cancellationToken);
        McpToolLog.Information(logger, tool, new { fromDate = from, toDate = to }, timer);
        return Json(summary);
    }

    [McpServerTool(Name = "top_impersonated_companies")]
    [Description("Ranking (join of Companies and ScamReports): companies ranked by the number of APPROVED scam reports that impersonated them, highest first, with total money requested. Optional start date.")]
    public async Task<string> TopImpersonatedCompanies(
        [Description("Number of companies to return. Defaults to 5 and is clamped to 1–10.")] int count = 5,
        [Description("Optional inclusive start date, exactly yyyy-MM-dd. Only approved reports on or after this date are counted.")] string? fromDate = null,
        CancellationToken cancellationToken = default)
    {
        const string tool = "top_impersonated_companies";
        var timer = McpToolLog.Start();
        if (!TryDate(fromDate, "fromDate", out var from, out var error))
        {
            McpToolLog.Information(logger, tool, new { count, fromDate }, timer);
            return error!;
        }

        var take = Math.Clamp(count, 1, 10);
        var companies = await repository.TopImpersonatedCompaniesAsync(take, from, cancellationToken);
        McpToolLog.Information(logger, tool, new { count = take, fromDate = from }, timer);
        return Json(new
        {
            count = take,
            fromDate = from,
            resultCount = companies.Count,
            companies,
        });
    }

    [McpServerTool(Name = "check_recruiter_email")]
    [Description("Risk check for one recruiter email address: compares the domain with official company domains, detects look-alike domains and free email providers, and counts approved scam reports. Returns a 0–100 risk score, a LOW/MEDIUM/HIGH verdict, and the reasons.")]
    public async Task<string> CheckRecruiterEmail(
        [Description("Full recruiter email address, e.g. hr@amaz0n-careers.net.")] string email,
        CancellationToken cancellationToken)
    {
        const string tool = "check_recruiter_email";
        var timer = McpToolLog.Start();
        if (!TryEmail(email, out var trimmed, out var error))
        {
            McpToolLog.Information(logger, tool, new { email = McpToolLog.RedactEmail(email), domain = DomainNormalizer.DomainOfEmail(email ?? "") }, timer);
            return error;
        }

        var result = await riskService.CheckAsync(trimmed, cancellationToken);
        McpToolLog.Information(logger, tool, new { email = McpToolLog.RedactEmail(trimmed), domain = DomainNormalizer.DomainOfEmail(trimmed) }, timer);
        return Json(result);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private static bool TryEmail(string? email, out string trimmed, out string error)
    {
        trimmed = email?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            error = "Email is required.";
            return false;
        }

        if (trimmed.Length > 254)
        {
            error = "Email must be 254 characters or fewer.";
            return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^[^@\\s]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$"))
        {
            error = "Email must be a full address such as hr@amaz0n-careers.net.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryChoice(string? value, string[] allowed, string name, out string? canonical, out string? error)
    {
        canonical = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var match = allowed.FirstOrDefault(item => item.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            error = $"{name} must be one of: {string.Join(", ", allowed)}.";
            return false;
        }

        canonical = match;
        return true;
    }

    private static bool TryDateRange(string? fromDate, string? toDate, out string? from, out string? to, out string? error)
    {
        if (!TryDate(fromDate, "fromDate", out from, out error))
        {
            to = null;
            return false;
        }

        if (!TryDate(toDate, "toDate", out to, out error))
        {
            return false;
        }

        if (from is not null && to is not null && string.CompareOrdinal(from, to) > 0)
        {
            error = "fromDate must be on or before toDate.";
            return false;
        }

        return true;
    }

    private static bool TryDate(string? value, string name, out string? iso, out string? error)
    {
        iso = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var text = value.Trim();
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            error = $"{name} must be an exact date in yyyy-MM-dd form.";
            return false;
        }

        iso = text;
        return true;
    }
}
