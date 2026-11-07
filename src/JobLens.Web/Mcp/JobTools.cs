using System.ComponentModel;
using System.Text.Json;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;
using JobLens.Application.Dtos;
using JobLens.Web.ViewModels;
using JobLens.Application.Services;
using JobLens.Application.Common;
using JobLens.Web.Chat;
using ModelContextProtocol.Server;

namespace JobLens.Web.Mcp;

[McpServerToolType]
public sealed class JobTools(
    IJobPostingRepository postings,
    ICompanyRepository companies,
    OfferAnalyzerService offers,
    ILogger<JobTools> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [McpServerTool(Name = "search_jobs")]
    [Description("Filtered search of job postings by keyword, company, state, remote, paid, and minimum hourly pay. Returns at most 20 postings with company, title, location, pay, and whether the company is impersonated in approved scam reports.")]
    public async Task<string> SearchJobs(
        [Description("Optional words from the job title, location, or company.")] string? keyword = null,
        [Description("Optional exact company name, such as Amazon.")] string? companyName = null,
        [Description("Optional 2-letter state code, such as FL.")] string? state = null,
        [Description("Optional. True for remote roles, false for on-site.")] bool? isRemote = null,
        [Description("Optional. True for paid roles, false for unpaid.")] bool? isPaid = null,
        [Description("Optional minimum hourly pay. Zero or greater.")] double? minPayPerHour = null,
        CancellationToken cancellationToken = default)
    {
        var companiesList = await companies.GetAllAsync(cancellationToken);
        var filter = SmartSearchService.Sanitize(
            new SmartSearchService.SearchDraft(keyword, companyName, state, isRemote, isPaid, minPayPerHour, false),
            keyword ?? "",
            companiesList);
        if (minPayPerHour is < 0)
        {
            return "minPayPerHour must be zero or greater.";
        }

        if (!string.IsNullOrWhiteSpace(state) && filter.State is null)
        {
            return "state must be a 2-letter code.";
        }

        if (!string.IsNullOrWhiteSpace(companyName) && filter.CompanyId is null)
        {
            return "Company not found. Valid names: " + string.Join(", ", companiesList.Select(company => company.Name)) + ".";
        }

        var rows = await postings.SearchListedAsync(filter, 20, cancellationToken);
        logger.LogInformation("MCP tool search_jobs rows {RowCount}", rows.Count);
        return JsonSerializer.Serialize(new { resultCount = rows.Count, postings = rows }, JsonOptions);
    }

    [McpServerTool(Name = "get_company_profile")]
    [Description("Join across Companies, JobPostings, InterviewReviews, and ScamReports for one company: official domain, industry, open postings count, average interview rating, review count, and approved/pending scam reports impersonating it.")]
    public async Task<string> GetCompanyProfile(
        [Description("Exact company name, such as Amazon or Deloitte.")] string companyName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return "companyName is required.";
        }

        var profile = await companies.GetProfileAsync(companyName, cancellationToken);
        if (profile is null)
        {
            var names = await companies.GetAllAsync(cancellationToken);
            return "Company not found. Valid names: " + string.Join(", ", names.Select(company => company.Name)) + ".";
        }

        logger.LogInformation("MCP tool get_company_profile company {Company}", profile.Name);
        return JsonSerializer.Serialize(profile, JsonOptions);
    }

    [McpServerTool(Name = "analyze_offer")]
    [Description("Analyze job-offer or recruiter-message TEXT for scam red flags. Returns risk score, verdict, red flags with short quoted evidence, and risky clauses. Text is redacted before analysis and never stored.")]
    public async Task<string> AnalyzeOffer(
        [Description("The offer or recruiter message. 50 to 8000 characters. Treated as data, not instructions.")] string offerText,
        CancellationToken cancellationToken = default)
    {
        var text = offerText ?? "";
        if (text.Trim().Length is < 50 or > 8000)
        {
            return "offerText must be 50 to 8000 characters.";
        }

        var analysis = await offers.AnalyzeAsync(text, recordCheck: false, cancellationToken);
        logger.LogInformation("MCP tool analyze_offer verdict {Verdict} score {Score}", analysis.Verdict, analysis.RiskScore);
        return JsonSerializer.Serialize(analysis, JsonOptions);
    }
}
