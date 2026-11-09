using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Application.Services;
using JobLens.Infrastructure.Persistence;
using Xunit;

namespace JobLens.Tests;

public sealed class LiveJobsTests
{
    [Fact]
    public void Keyword_filter_matches_title_case_insensitively()
    {
        var jobs = new List<LiveJob>
        {
            new("1", "Data Intern", "Amazon", "Tampa, FL", "Internship", "Data Science", "x", null, "https://example.com/1"),
            new("2", "Software Engineer", "Meta", "Remote", "Mid Level", "Software Engineering", "y", null, "https://example.com/2"),
        };

        var filtered = LiveJobsService.FilterByKeyword(jobs, "intern");
        Assert.Single(filtered);
        Assert.Equal("Data Intern", filtered[0].Title);
    }

    [Fact]
    public async Task Trust_matches_company_suffixes_and_counts_approved_reports()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var recruiters = new RecruiterRepository(database.ConnectionString);
        var companyService = new CompanyService(companies);
        var reportService = new ScamReportService(reports, companies, recruiters);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var student = await TestActors.CreateStudentAsync(database.ConnectionString);

        var amazon = await companyService.CreateAsync(new CompanyFormVm
        {
            Name = "Amazon",
            OfficialDomain = "amazon.com",
            Industry = "Technology",
            HqState = "WA",
        }, moderator, CancellationToken.None);
        Assert.True(amazon.Success);

        var created = await reportService.CreateAsync(new ScamReportFormVm
        {
            CompanyId = amazon.Id!.Value,
            RecruiterEmail = "hr@amaz0n-careers.net",
            ScamType = "Fake check",
            Channel = "Email",
            AmountRequestedUsd = 100,
            DomainAgeDays = 10,
            Description = "Fake Amazon check scam report used for live-jobs trust tests.",
        }, student, CancellationToken.None);
        Assert.True((await reportService.ApproveAsync(created.Id!.Value, moderator, CancellationToken.None)).Success);

        var service = new LiveJobsService(new StubLiveJobsProvider([]), companies);
        var all = await companies.GetAllAsync(CancellationToken.None);
        var (matched, companyId) = await service.BuildTrustAsync("Amazon Inc", all, CancellationToken.None);
        Assert.Equal(amazon.Id, companyId);
        Assert.Equal("Amazon", matched.MatchedCompany);
        Assert.Equal("amazon.com", matched.OfficialDomain);
        Assert.Equal(1, matched.ApprovedScamReports);
        Assert.Contains(matched.Notes, note => note.Contains("@amazon.com", StringComparison.Ordinal));
        Assert.Contains(matched.Notes, note => note.Contains("1 approved scam reports", StringComparison.Ordinal));

        var (unmatched, unmatchedId) = await service.BuildTrustAsync("Totally Unknown Co", all, CancellationToken.None);
        Assert.Null(unmatchedId);
        Assert.Null(unmatched.MatchedCompany);
        Assert.Equal(0, unmatched.ApprovedScamReports);
        Assert.Contains(unmatched.Notes, note => note.Contains("Not in the JobLens company list", StringComparison.Ordinal));
    }

    private sealed class StubLiveJobsProvider(IReadOnlyList<LiveJob> items) : ILiveJobsProvider
    {
        public string Name => "Test provider";
        public string AttributionText => "test";
        public string AttributionUrl => "https://example.com";

        public Task<LiveJobsResult> SearchAsync(LiveJobsQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new LiveJobsResult(items, query.Page, 1, null, false));
    }
}
