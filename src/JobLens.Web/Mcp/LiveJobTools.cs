using System.ComponentModel;
using System.Text.Json;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Application.Services;
using ModelContextProtocol.Server;

namespace JobLens.Web.Mcp;

[McpServerToolType]
public sealed class LiveJobTools(
    LiveJobsService liveJobs,
    ILiveJobsProvider provider,
    ILogger<LiveJobTools> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    [McpServerTool(Name = "search_live_jobs")]
    [Description("LIVE external job listings (USAJOBS or The Muse, depending on Jobs:Provider) with JobLens trust signals. Returns at most 10 openings. Live listings are not verified by JobLens; scam facts must still come from database tools.")]
    public async Task<string> SearchLiveJobs(
        [Description("Optional keyword (title/summary search for USAJOBS; title filter for The Muse).")] string? keyword = null,
        [Description("Optional location text, e.g. Tampa, FL.")] string? location = null,
        [Description("Optional USAJOBS series code (e.g. 1560) or Muse category name.")] string? category = null,
        [Description("Optional Muse level: Internship, Entry Level, Mid Level, or Senior Level.")] string? level = null,
        [Description("Optional USAJOBS hiring path: student, graduates, or public.")] string? hiringPath = null,
        [Description("When true, only remote USAJOBS openings.")] bool remoteOnly = false,
        CancellationToken cancellationToken = default)
    {
        const string tool = "search_live_jobs";
        var timer = McpToolLog.Start();
        var page = await liveJobs.SearchAsync(
            new LiveJobsQuery(
                keyword,
                location,
                category,
                CategoryCode: category is { Length: > 0 } && category.All(char.IsDigit) ? category : null,
                level,
                hiringPath,
                remoteOnly,
                CompanyName: null,
                Page: 1),
            cancellationToken);
        var items = page.Items.Take(10).Select(listing => new
        {
            listing.Job.Id,
            listing.Job.Title,
            listing.Job.CompanyName,
            listing.Job.Locations,
            listing.Job.Levels,
            listing.Job.Categories,
            listing.Job.PayRange,
            listing.Job.DescriptionSnippet,
            listing.Job.PublishedDate,
            listing.Job.CloseDate,
            listing.Job.ApplyUrl,
            listing.Job.IsRemote,
            listing.Job.Source,
            trust = new
            {
                listing.Trust.MatchedCompany,
                listing.Trust.OfficialDomain,
                listing.Trust.ApprovedScamReports,
                listing.Trust.IsOfficialGovSource,
                listing.Trust.Notes,
            },
        }).ToList();

        McpToolLog.Information(
            logger,
            tool,
            new { keyword, location, category, level, hiringPath, remoteOnly, provider = provider.Name },
            timer);

        return JsonSerializer.Serialize(new
        {
            source = provider.Name + " (live external)",
            attribution = provider.AttributionText,
            disclaimer = "Live listings are not verified by JobLens. Use database tools for scam-report facts.",
            message = page.Message,
            fromCache = page.FromCache,
            resultCount = items.Count,
            jobs = items,
        }, JsonOptions);
    }
}
