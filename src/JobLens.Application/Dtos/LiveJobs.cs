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
    string Source = "The Muse",
    string? CloseDate = null,
    string? PayRange = null,
    bool IsRemote = false);

public sealed record LiveJobsQuery(
    string? Keyword = null,
    string? Location = null,
    string? Category = null,
    string? CategoryCode = null,
    string? Level = null,
    string? HiringPath = null,
    bool RemoteOnly = false,
    string? CompanyName = null,
    int Page = 1);

/// <summary>Legacy Muse-shaped search; prefer <see cref="LiveJobsQuery"/>.</summary>
public sealed record LiveJobSearch(
    string? Category = null,
    string? Level = null,
    string? Location = null,
    string? CompanyName = null,
    string? Keyword = null,
    int Page = 1)
{
    public LiveJobsQuery ToQuery() =>
        new(Keyword, Location, Category, CategoryCode: null, Level, HiringPath: null, RemoteOnly: false, CompanyName, Page);
}

public sealed record LiveJobsResult(
    IReadOnlyList<LiveJob> Items,
    int Page,
    int PageCount,
    string? Message,
    bool FromCache,
    string? DebugDetail = null);

/// <summary>Alias kept for Muse client tests during the provider migration.</summary>
public sealed record LiveJobResult(
    IReadOnlyList<LiveJob> Items,
    int Page,
    int PageCount,
    string? Message,
    bool FromCache,
    string? DebugDetail = null)
{
    public LiveJobsResult ToProviderResult() =>
        new(Items, Page, PageCount, Message, FromCache, DebugDetail);

    public static LiveJobResult FromProvider(LiveJobsResult result) =>
        new(result.Items, result.Page, result.PageCount, result.Message, result.FromCache, result.DebugDetail);
}

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
