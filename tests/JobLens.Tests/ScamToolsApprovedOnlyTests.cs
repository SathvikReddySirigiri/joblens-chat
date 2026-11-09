using System.Text.Json;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Application.Services;
using JobLens.Domain.Constants;
using JobLens.Domain.Entities;
using JobLens.Web.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class ScamToolsApprovedOnlyTests
{
    [Fact]
    public async Task GetDomainReport_returns_approved_report_details_only()
    {
        var repo = new StubQueryRepo(
        [
            Report(1, "Pending"),
            Report(2, ReportStatus.Approved),
            Report(3, ReportStatus.Rejected),
            Report(4, ReportStatus.Approved),
        ]);
        var tools = CreateTools(repo);

        var json = await tools.GetDomainReport("amaz0n-careers.net", CancellationToken.None);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("reportCount").GetInt32());
        foreach (var report in doc.RootElement.GetProperty("reports").EnumerateArray())
        {
            Assert.Equal(ReportStatus.Approved, report.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task SearchScamReports_forces_approved_status_for_details()
    {
        var repo = new StubQueryRepo([]);
        var tools = CreateTools(repo);

        var json = await tools.SearchScamReports(cancellationToken: CancellationToken.None);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(ReportStatus.Approved, doc.RootElement.GetProperty("filters").GetProperty("status").GetString());
        Assert.Equal(ReportStatus.Approved, repo.LastSearchStatus);
    }

    [Fact]
    public async Task SearchScamReports_returns_no_details_for_pending_status()
    {
        var repo = new StubQueryRepo([Report(1, ReportStatus.Pending)]);
        var tools = CreateTools(repo);

        var json = await tools.SearchScamReports(status: ReportStatus.Pending, cancellationToken: CancellationToken.None);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("resultCount").GetInt32());
        Assert.Null(repo.LastSearchStatus);
        Assert.Contains("approved", doc.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tool_descriptions_mention_approved_reports()
    {
        var domain = typeof(ScamTools).GetMethod(nameof(ScamTools.GetDomainReport))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), inherit: false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single()
            .Description;
        var search = typeof(ScamTools).GetMethod(nameof(ScamTools.SearchScamReports))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), inherit: false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single()
            .Description;

        Assert.Contains("approved", domain, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("approved", search, StringComparison.OrdinalIgnoreCase);
    }

    private static ScamTools CreateTools(StubQueryRepo repo) =>
        new(repo, new RecruiterRiskService(repo), NullLogger<ScamTools>.Instance);

    private static ScamReport Report(int id, string status) =>
        new(
            id,
            1,
            "Amazon",
            "hr@amaz0n-careers.net",
            "amaz0n-careers.net",
            "Fake check",
            "Email",
            500,
            30,
            status,
            "2026-03-01",
            "Synthetic");

    private sealed class StubQueryRepo(IReadOnlyList<ScamReport> reports) : IScamAnalyticsRepository
    {
        public string? LastSearchStatus { get; private set; }

        public Task<IReadOnlyList<ScamReport>> GetReportsByDomainAsync(string domain, CancellationToken cancellationToken) =>
            Task.FromResult(reports);

        public Task<IReadOnlyList<ScamReport>> SearchReportsAsync(
            string? scamType,
            string? companyName,
            string? channel,
            string? status,
            string? fromDate,
            string? toDate,
            CancellationToken cancellationToken)
        {
            LastSearchStatus = status;
            return Task.FromResult<IReadOnlyList<ScamReport>>(
                reports.Where(report => status is null || report.Status == status).ToList());
        }

        public Task<ScamSummary> SummarizeAsync(string? fromDate, string? toDate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CompanyRanking>> TopImpersonatedCompaniesAsync(
            int count,
            string? fromDate,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Company>>([]);

        public Task<int> CountApprovedReportsForDomainAsync(string domain, CancellationToken cancellationToken) =>
            Task.FromResult(reports.Count(report => report.Status == ReportStatus.Approved));

        public Task<DataCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<(int ApprovedReports, int CompaniesImpersonated, double AmountRequestedUsd, int RecruiterChecks)> GetHomeImpactStatsAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ScamReport>> GetLatestApprovedReportsAsync(int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountPendingReportsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountReportsSinceAsync(string fromDateInclusive, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
