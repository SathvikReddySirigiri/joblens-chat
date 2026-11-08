using System.Text.RegularExpressions;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

public sealed partial class LiveJobsService(ILiveJobsProvider provider, ICompanyRepository companies)
{
    public const string UsaJobsProviderName = "USAJOBS";
    public const string MuseProviderName = "The Muse";

    public static readonly string[] AllowedLevels =
    [
        "Internship",
        "Entry Level",
        "Mid Level",
        "Senior Level",
    ];

    public static readonly string[] AllowedCategories =
    [
        "Data and Analytics",
        "Data Science",
        "Software Engineering",
        "Accounting and Finance",
        "Business Operations",
        "Marketing",
        "Project Management",
        "IT",
        "Healthcare",
    ];

    public static readonly IReadOnlyList<(string Label, string Code)> UsaJobsCategories =
    [
        ("Data Science", "1560"),
        ("Statistics", "1530"),
        ("Operations Research", "1515"),
        ("Management & Program Analysis", "0343"),
        ("IT", "2210"),
        ("Accounting", "0510"),
        ("Financial Analysis", "1160"),
        ("Economics", "0110"),
        ("Business & Industry", "1101"),
    ];

    public bool IsUsaJobs =>
        provider.Name.Contains("USAJOBS", StringComparison.OrdinalIgnoreCase)
        || provider.Name.Contains("USA Jobs", StringComparison.OrdinalIgnoreCase);

    public string ProviderName => provider.Name;

    public string AttributionText => provider.AttributionText;

    public string AttributionUrl => provider.AttributionUrl;

    public async Task<LiveJobsPage> SearchAsync(LiveJobsQuery search, CancellationToken cancellationToken)
    {
        var normalized = Normalize(search, out var error);
        if (error is not null)
        {
            return EmptyPage(normalized, error);
        }

        var raw = await provider.SearchAsync(normalized, cancellationToken);
        var keyword = normalized.Keyword?.Trim() ?? "";
        // Muse keyword is client-side; USAJOBS already applied Keyword server-side.
        var items = !IsUsaJobs && keyword.Length > 0
            ? raw.Items.Where(job => job.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList()
            : raw.Items;

        var companyList = await companies.GetAllAsync(cancellationToken);
        var listings = new List<LiveJobListing>(items.Count);
        foreach (var job in items)
        {
            var safeJob = job with { ApplyUrl = SafeApplyUrl(job.ApplyUrl, job.Source) ?? "" };
            var (trust, companyId) = await BuildTrustAsync(safeJob, companyList, cancellationToken);
            listings.Add(new LiveJobListing(safeJob, trust, companyId));
        }

        return new LiveJobsPage(
            listings,
            raw.Page,
            Math.Clamp(raw.PageCount, 1, 5),
            raw.Message,
            raw.FromCache,
            normalized,
            provider.Name,
            provider.AttributionText,
            provider.AttributionUrl,
            raw.DebugDetail);
    }

    /// <summary>Hides apply links that do not point at usajobs.gov for USAJOBS listings.</summary>
    internal static string? SafeApplyUrl(string? url, string source)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        var isUsaJobs = source.Contains("USAJOBS", StringComparison.OrdinalIgnoreCase)
            || source.Contains("USA Jobs", StringComparison.OrdinalIgnoreCase);
        if (!isUsaJobs)
        {
            return uri.ToString();
        }

        var host = uri.Host.TrimEnd('.');
        if (host.Equals("usajobs.gov", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".usajobs.gov", StringComparison.OrdinalIgnoreCase))
        {
            return uri.ToString();
        }

        return null;
    }

    internal static LiveJobsQuery Normalize(LiveJobsQuery search, out string? error, bool usaJobs = false)
    {
        error = null;
        var page = Math.Clamp(search.Page <= 0 ? 1 : search.Page, 1, 5);

        var location = search.Location?.Trim() ?? "";
        if (location.Length > 60)
        {
            error = "Location must be 60 characters or fewer.";
            return search with { Page = page };
        }

        var keyword = search.Keyword?.Trim() ?? "";
        if (keyword.Length > 60)
        {
            error = "Keyword must be 60 characters or fewer.";
            return search with { Page = page, Location = NullIfEmpty(location) };
        }

        if (usaJobs)
        {
            string? categoryCode = null;
            string? categoryLabel = null;
            if (!string.IsNullOrWhiteSpace(search.CategoryCode))
            {
                var match = UsaJobsCategories.FirstOrDefault(item =>
                    item.Code.Equals(search.CategoryCode.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match.Code is null)
                {
                    error = "Choose a category from the supported USAJOBS series list.";
                    return search with { Page = page, Location = NullIfEmpty(location), Keyword = NullIfEmpty(keyword) };
                }

                categoryCode = match.Code;
                categoryLabel = match.Label;
            }
            else if (!string.IsNullOrWhiteSpace(search.Category))
            {
                var match = UsaJobsCategories.FirstOrDefault(item =>
                    item.Label.Equals(search.Category.Trim(), StringComparison.OrdinalIgnoreCase)
                    || item.Code.Equals(search.Category.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match.Code is null)
                {
                    error = "Choose a category from the supported USAJOBS series list.";
                    return search with { Page = page, Location = NullIfEmpty(location), Keyword = NullIfEmpty(keyword) };
                }

                categoryCode = match.Code;
                categoryLabel = match.Label;
            }

            var hiringPath = string.IsNullOrWhiteSpace(search.HiringPath)
                ? "student"
                : search.HiringPath.Trim().ToLowerInvariant();
            if (hiringPath is not ("student" or "graduates" or "public"))
            {
                error = "Hiring path must be Students & interns, Recent graduates, or open to the public.";
                return search with { Page = page, Location = NullIfEmpty(location), Keyword = NullIfEmpty(keyword) };
            }

            return new LiveJobsQuery(
                NullIfEmpty(keyword),
                NullIfEmpty(location),
                categoryLabel,
                categoryCode,
                Level: null,
                hiringPath,
                search.RemoteOnly,
                CompanyName: null,
                page);
        }

        var level = string.IsNullOrWhiteSpace(search.Level) ? "Internship" : search.Level.Trim();
        if (!AllowedLevels.Contains(level, StringComparer.OrdinalIgnoreCase))
        {
            error = "Level must be Internship, Entry Level, Mid Level, or Senior Level.";
            return search with { Page = page, Level = "Internship" };
        }

        level = AllowedLevels.First(item => item.Equals(level, StringComparison.OrdinalIgnoreCase));

        string? category = null;
        if (!string.IsNullOrWhiteSpace(search.Category))
        {
            category = AllowedCategories.FirstOrDefault(item =>
                item.Equals(search.Category.Trim(), StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                error = "Choose a category from the supported JobLens list.";
                return search with { Page = page, Level = level };
            }
        }

        var company = search.CompanyName?.Trim() ?? "";
        if (company.Length > 80)
        {
            company = company[..80];
        }

        return new LiveJobsQuery(
            NullIfEmpty(keyword),
            NullIfEmpty(location),
            category,
            CategoryCode: null,
            level,
            HiringPath: null,
            RemoteOnly: false,
            NullIfEmpty(company),
            page);
    }

    internal LiveJobsQuery Normalize(LiveJobsQuery search, out string? error) =>
        Normalize(search, out error, IsUsaJobs);

    internal static string NormalizeCompanyName(string name)
    {
        var value = (name ?? "").Trim();
        value = CorporateSuffixPattern().Replace(value, " ");
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    internal async Task<(JobTrust Trust, int? CompanyId)> BuildTrustAsync(
        LiveJob job,
        IReadOnlyList<Company> companyList,
        CancellationToken cancellationToken)
    {
        var isOfficial = job.Source.Contains("USAJOBS", StringComparison.OrdinalIgnoreCase)
            || job.Source.Contains("USA Jobs", StringComparison.OrdinalIgnoreCase);

        if (isOfficial)
        {
            return (
                new JobTrust(
                    MatchedCompany: job.CompanyName,
                    OfficialDomain: "usajobs.gov",
                    ApprovedScamReports: 0,
                    Notes:
                    [
                        "Real federal jobs never charge application or \"processing\" fees, and USAJOBS never asks you to pay — beware messages that claim to be from USAJOBS asking for money or your SSN by email/chat.",
                    ],
                    IsOfficialGovSource: true),
                null);
        }

        return await BuildCompanyTrustAsync(job.CompanyName, companyList, cancellationToken);
    }

    // Overload used by existing unit tests that pass a company name only.
    internal Task<(JobTrust Trust, int? CompanyId)> BuildTrustAsync(
        string companyName,
        IReadOnlyList<Company> companyList,
        CancellationToken cancellationToken) =>
        BuildCompanyTrustAsync(companyName, companyList, cancellationToken);

    private async Task<(JobTrust Trust, int? CompanyId)> BuildCompanyTrustAsync(
        string companyName,
        IReadOnlyList<Company> companyList,
        CancellationToken cancellationToken)
    {
        var needle = NormalizeCompanyName(companyName);
        Company? match = null;
        foreach (var company in companyList)
        {
            if (NormalizeCompanyName(company.Name).Equals(needle, StringComparison.OrdinalIgnoreCase))
            {
                match = company;
                break;
            }
        }

        if (match is null)
        {
            return (
                new JobTrust(
                    null,
                    null,
                    0,
                    ["Not in the JobLens company list — verify on the company's official careers page."]),
                null);
        }

        var activity = await companies.GetActivityAsync(match.CompanyId, cancellationToken);
        var approved = activity.ApprovedReports;
        var notes = new List<string>
        {
            $"Only trust recruiters using @{match.OfficialDomain}",
        };
        if (approved > 0)
        {
            notes.Add($"{approved} approved scam reports impersonate {match.Name}");
        }

        return (new JobTrust(match.Name, match.OfficialDomain, approved, notes), match.CompanyId);
    }

    internal static IReadOnlyList<LiveJob> FilterByKeyword(IReadOnlyList<LiveJob> items, string? keyword)
    {
        var term = keyword?.Trim() ?? "";
        if (term.Length == 0)
        {
            return items;
        }

        return items.Where(job => job.Title.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private LiveJobsPage EmptyPage(LiveJobsQuery search, string error) =>
        new(
            [],
            1,
            1,
            error,
            false,
            search,
            provider.Name,
            provider.AttributionText,
            provider.AttributionUrl);

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    [GeneratedRegex(@"\b(inc\.?|llc\.?|corp\.?|co\.?|ltd\.?|limited|incorporated)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CorporateSuffixPattern();
}
