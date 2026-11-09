namespace JobLens.Application.Dtos;

public sealed record LiveJob(
    string Id,
    string Title,
    string CompanyName,
    string Locations,
    string Levels,
    string Categories,
    string DescriptionSnippet,
    string? PublishedDate,
    string ApplyUrl,
    string Source = "USAJOBS",
    string? CloseDate = null,
    string? PayRange = null,
    bool IsRemote = false);

public sealed record LiveJobsQuery(
    string? Keyword = null,
    string? Location = null,
    string? Category = null,
    string? CategoryCode = null,
    string? HiringPath = null,
    bool RemoteOnly = false,
    int Page = 1);

public sealed record LiveJobsResult(
    IReadOnlyList<LiveJob> Items,
    int Page,
    int PageCount,
    string? Message,
    bool FromCache,
    string? DebugDetail = null);

public sealed record JobTrust(
    string? MatchedCompany,
    string? OfficialDomain,
    int ApprovedScamReports,
    IReadOnlyList<string> Notes,
    bool IsOfficialGovSource = false);

public sealed record LiveJobListing(
    LiveJob Job,
    JobTrust Trust,
    int? MatchedCompanyId);

public sealed record LiveJobsPage(
    IReadOnlyList<LiveJobListing> Items,
    int Page,
    int PageCount,
    string? Message,
    bool FromCache,
    LiveJobsQuery Search,
    string ProviderName,
    string AttributionText,
    string AttributionUrl,
    string? DebugDetail = null);
