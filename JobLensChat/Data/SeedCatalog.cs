using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobLensChat.Data;

internal static class SeedCatalog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Loaded once from the embedded file so Azure gets the same rows as a local build.
    internal static SeedDocument Document { get; } = Load();

    private static SeedDocument Load()
    {
        var assembly = typeof(SeedCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("JobLensChat.Data.Seed.seed-data.json")
            ?? throw new InvalidOperationException("Embedded seed file JobLensChat.Data.Seed.seed-data.json was not found.");
        var document = JsonSerializer.Deserialize<SeedDocument>(stream, Options)
            ?? throw new InvalidOperationException("Embedded seed file was empty.");
        EnsureRules(document);
        return document;
    }

    private static void EnsureRules(SeedDocument seed)
    {
        var problems = new List<string>();
        if (seed.Companies.Count != 20)
        {
            problems.Add($"Expected 20 companies, found {seed.Companies.Count}.");
        }

        if (seed.Reports.Count != 120)
        {
            problems.Add($"Expected 120 reports, found {seed.Reports.Count}.");
        }

        if (seed.JobPostings.Count != 70 || seed.InterviewReviews.Count != 50 || seed.CheckLogs.Count != 100)
        {
            problems.Add("Posting, review, or check-log seed length is wrong.");
        }

        if (seed.OfficialRecruiters.Count != 10 || seed.Users.Count != 5)
        {
            problems.Add("Official recruiter or user seed length is wrong.");
        }

        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var report in seed.Reports)
        {
            if (!emails.Add(report.RecruiterEmail))
            {
                problems.Add($"Duplicate recruiter email {report.RecruiterEmail}.");
            }
        }

        var approved = seed.Reports.Count(report => report.Status == "Approved");
        var pending = seed.Reports.Count(report => report.Status == "Pending");
        var rejected = seed.Reports.Count(report => report.Status == "Rejected");
        if (approved != 85 || pending != 23 || rejected != 12)
        {
            problems.Add($"Status totals are {approved}/{pending}/{rejected}.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Scam report seed is inconsistent: " + string.Join(" ", problems));
        }
    }
}

internal sealed class SeedDocument
{
    public List<CompanySeedRow> Companies { get; init; } = [];

    public List<ReportSeedRow> Reports { get; init; } = [];

    public List<OfficialRecruiterSeedRow> OfficialRecruiters { get; init; } = [];

    public List<PostingSeedRow> JobPostings { get; init; } = [];

    public List<ReviewSeedRow> InterviewReviews { get; init; } = [];

    public List<CheckLogSeedRow> CheckLogs { get; init; } = [];

    public List<UserSeedRow> Users { get; init; } = [];
}

internal sealed class CompanySeedRow
{
    public string Name { get; init; } = "";

    public string OfficialDomain { get; init; } = "";

    public string Industry { get; init; } = "";

    public string HqState { get; init; } = "";
}

internal sealed class ReportSeedRow
{
    public string CompanyName { get; init; } = "";

    public string RecruiterEmail { get; init; } = "";

    public string ScamType { get; init; } = "";

    public string Channel { get; init; } = "";

    public double AmountRequestedUsd { get; init; }

    public int DomainAgeDays { get; init; }

    public string Status { get; init; } = "";

    public string ReportedDate { get; init; } = "";

    public string Description { get; init; } = "";
}

internal sealed class OfficialRecruiterSeedRow
{
    public string Email { get; init; } = "";

    public string CompanyDomain { get; init; } = "";

    public string FirstSeen { get; init; } = "";

    public string Notes { get; init; } = "";
}

internal sealed class PostingSeedRow
{
    public string CompanyName { get; init; } = "";

    public string Title { get; init; } = "";

    public string Location { get; init; } = "";

    public string State { get; init; } = "";

    public int IsRemote { get; init; }

    public int IsPaid { get; init; }

    public double? PayRatePerHour { get; init; }

    public string PostedDate { get; init; } = "";

    public string Description { get; init; } = "";
}

internal sealed class ReviewSeedRow
{
    public string CompanyName { get; init; } = "";

    public string Role { get; init; } = "";

    public int Rating { get; init; }

    public string Experience { get; init; } = "";

    public int WasLegit { get; init; }

    public string ReviewDate { get; init; } = "";
}

internal sealed class CheckLogSeedRow
{
    public string CheckType { get; init; } = "";

    public string InputSummary { get; init; } = "";

    public int RiskScore { get; init; }

    public string Verdict { get; init; } = "";

    public string CreatedAt { get; init; } = "";
}

internal sealed class UserSeedRow
{
    public string Email { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Role { get; init; } = "";

    [JsonPropertyName("passwordSlot")]
    public string PasswordSlot { get; init; } = "";
}
