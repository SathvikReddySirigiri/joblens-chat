using System.Globalization;
using JobLens.Application.Dtos.Dashboard;
using JobLens.Application.Services;
using JobLens.Domain.Entities;
using JobLens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task Seeded_kpis_match_known_totals()
    {
        await using var database = await TempDatabase.CreateSeededAsync();
        var data = await Service(database).GetDashboardAsync("all", CancellationToken.None);

        Assert.Equal(120, data.Kpis.TotalReports);
        Assert.Equal(23, data.Kpis.PendingReports);
        Assert.Equal(85, data.Kpis.ApprovedReports);
        Assert.Equal(87.6, data.Kpis.ApprovalRate);
        Assert.Equal(94026.00, data.Kpis.TotalAmountRequestedUsd);
    }

    [Fact]
    public async Task Seeded_top_impersonated_order_and_scam_types()
    {
        await using var database = await TempDatabase.CreateSeededAsync();
        var data = await Service(database).GetDashboardAsync("all", CancellationToken.None);

        Assert.Equal(
            ["Amazon", "Microsoft", "Deloitte", "Google", "JPMorgan Chase"],
            data.TopImpersonatedCompanies.Take(5).Select(row => row.CompanyName).ToArray());
        Assert.Equal([15, 13, 10, 9, 7], data.TopImpersonatedCompanies.Take(5).Select(row => row.ApprovedReports).ToArray());

        Assert.Equal(
            [
                ("Fake check", 23),
                ("Upfront fee", 21),
                ("Chat-only interview", 20),
                ("Equipment purchase", 20),
                ("Fake staffing agency", 19),
                ("Identity theft", 17),
            ],
            data.ReportsByScamType.Select(row => (row.Category, row.Count)).ToArray());
    }

    [Fact]
    public async Task Range_3m_only_includes_months_within_three_months_of_latest_report()
    {
        await using var database = await TempDatabase.CreateSeededAsync();
        var data = await Service(database).GetDashboardAsync("3m", CancellationToken.None);
        var latest = DateOnly.ParseExact(data.LatestReportedDate!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var earliestAllowed = latest.AddMonths(-3);

        Assert.Equal("3m", data.Range);
        Assert.NotEmpty(data.ReportsPerMonth);
        foreach (var month in data.ReportsPerMonth)
        {
            var start = DateOnly.ParseExact(month.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Assert.True(start >= new DateOnly(earliestAllowed.Year, earliestAllowed.Month, 1));
            Assert.True(start <= latest);
        }
    }

    [Fact]
    public async Task Seeded_insights_quote_dto_numbers()
    {
        await using var database = await TempDatabase.CreateSeededAsync();
        var data = await Service(database).GetDashboardAsync("all", CancellationToken.None);
        var texts = data.Insights.Select(insight => insight.Text).ToList();
        var top = data.TopImpersonatedCompanies[0];
        var money = data.TopImpersonatedCompanies
            .OrderByDescending(row => row.TotalAmountRequestedUsd)
            .First();

        Assert.Contains(texts, text => text.Contains(top.CompanyName, StringComparison.Ordinal)
            && text.Contains(top.ApprovedReports.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            && text.Contains(top.TotalAmountRequestedUsd.ToString("$#,##0.00", CultureInfo.InvariantCulture), StringComparison.Ordinal));
        Assert.Contains(texts, text => text.Contains(data.Kpis.PendingReports.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            && text.Contains("waiting for moderation", StringComparison.Ordinal));
        if (money.ApprovedReports < top.ApprovedReports)
        {
            Assert.Contains(texts, text => text.Contains(money.CompanyName, StringComparison.Ordinal)
                && text.Contains("Highest money requested", StringComparison.Ordinal));
        }

        Assert.Contains(texts, text => text.Contains("HIGH risk", StringComparison.Ordinal)
            || text.Contains("No recruiter checks", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Approval_rate_excludes_pending_and_uses_approved_over_decided()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var companyId = await companies.CreateAsync("Amazon", "amazon.com", "Technology", "WA", CancellationToken.None);
        await ReportAsync(reports, companyId, "Approved", "2026-10-01", 10, "a1@fake.test");
        await ReportAsync(reports, companyId, "Approved", "2026-10-02", 10, "a2@fake.test");
        await ReportAsync(reports, companyId, "Approved", "2026-10-03", 10, "a3@fake.test");
        await ReportAsync(reports, companyId, "Rejected", "2026-10-04", 5, "r1@fake.test");
        await ReportAsync(reports, companyId, "Pending", "2026-10-05", 100, "p1@fake.test");
        await ReportAsync(reports, companyId, "Pending", "2026-10-06", 100, "p2@fake.test");

        var snapshot = await Service(database).GetDashboardAsync("all", CancellationToken.None);

        Assert.Equal(75, snapshot.Kpis.ApprovalRate);
        Assert.Equal(3, snapshot.Kpis.ApprovedReports);
        Assert.Equal(2, snapshot.Kpis.PendingReports);
        Assert.Equal(235, snapshot.Kpis.TotalAmountRequestedUsd);
    }

    [Fact]
    public async Task Range_is_measured_from_the_latest_reported_date()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var companyId = await companies.CreateAsync("Google", "google.com", "Technology", "CA", CancellationToken.None);
        await ReportAsync(reports, companyId, "Approved", "2026-01-10", 20, "old@fake.test");
        await ReportAsync(reports, companyId, "Approved", "2026-10-20", 20, "new1@fake.test");
        await ReportAsync(reports, companyId, "Pending", "2026-10-21", 20, "new2@fake.test");

        var service = Service(database);
        var all = await service.GetDashboardAsync("all", CancellationToken.None);
        var six = await service.GetDashboardAsync("6m", CancellationToken.None);
        var three = await service.GetDashboardAsync("3m", CancellationToken.None);

        Assert.Equal(3, all.Kpis.TotalReports);
        Assert.Equal(2, six.Kpis.TotalReports);
        Assert.Equal(2, three.Kpis.TotalReports);
        Assert.Equal("2026-04-20", DashboardService.Window("6m", "2026-10-20").From);
        Assert.Equal("2026-07-20", DashboardService.Window("3m", "2026-10-20").From);
    }

    [Fact]
    public async Task Insights_quote_the_stored_counts_amounts_and_this_months_checks()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var logs = new CheckLogRepository(database.ConnectionString);
        var amazon = await companies.CreateAsync("Amazon", "amazon.com", "Technology", "WA", CancellationToken.None);
        await ReportAsync(reports, amazon, "Pending", "2026-07-04", 0, "july1@fake.test");
        await ReportAsync(reports, amazon, "Pending", "2026-07-18", 0, "july2@fake.test");
        await ReportAsync(reports, amazon, "Approved", "2026-10-02", 50, "oct1@fake.test");
        await ReportAsync(reports, amazon, "Approved", "2026-10-09", 50, "oct2@fake.test");
        await ReportAsync(reports, amazon, "Approved", "2026-10-16", 50, "oct3@fake.test");
        await ReportAsync(reports, amazon, "Pending", "2026-10-22", 0, "oct4@fake.test");

        await logs.AddAsync(new CheckLog(0, "Recruiter", "old.test", 80, "HIGH", "2026-01-15"), CancellationToken.None);
        await logs.AddAsync(new CheckLog(0, "Recruiter", "today.test", 90, "HIGH", "2026-10-28"), CancellationToken.None);
        await logs.AddAsync(new CheckLog(0, "Recruiter", "today.test", 10, "LOW", "2026-10-28"), CancellationToken.None);
        await logs.AddAsync(new CheckLog(0, "Recruiter", "today.test", 20, "LOW", "2026-10-29"), CancellationToken.None);
        await logs.AddAsync(new CheckLog(0, "Recruiter", "today.test", 30, "LOW", "2026-10-29"), CancellationToken.None);

        var snapshot = await Service(database).GetDashboardAsync("all", CancellationToken.None);
        var texts = snapshot.Insights.Select(insight => insight.Text).ToList();

        Assert.Equal(4, snapshot.Kpis.ChecksThisMonth);
        Assert.Equal(40, snapshot.Kpis.HighRiskCheckPct);
        Assert.Contains("Reports grew 100% from July to October.", texts);
        Assert.Contains("Amazon is the most impersonated company (3 approved reports, $150.00 requested).", texts);
        Assert.Contains("3 reports are waiting for moderation.", texts);
        Assert.Contains("25% of recruiter checks were HIGH risk this month.", texts);
    }

    [Fact]
    public async Task Top_companies_are_ordered_by_approved_reports()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var microsoft = await companies.CreateAsync("Microsoft", "microsoft.com", "Technology", "WA", CancellationToken.None);
        var amazon = await companies.CreateAsync("Amazon", "amazon.com", "Technology", "WA", CancellationToken.None);
        var walmart = await companies.CreateAsync("Walmart", "walmart.com", "Retail", "AR", CancellationToken.None);
        await ReportAsync(reports, microsoft, "Approved", "2026-08-01", 10, "m1@fake.test");
        await ReportAsync(reports, microsoft, "Approved", "2026-08-02", 10, "m2@fake.test");
        await ReportAsync(reports, microsoft, "Approved", "2026-08-03", 10, "m3@fake.test");
        await ReportAsync(reports, amazon, "Approved", "2026-08-04", 80, "a1@fake.test");
        await ReportAsync(reports, walmart, "Pending", "2026-08-05", 500, "w1@fake.test");
        await ReportAsync(reports, walmart, "Pending", "2026-08-06", 500, "w2@fake.test");
        await ReportAsync(reports, walmart, "Pending", "2026-08-07", 500, "w3@fake.test");
        await ReportAsync(reports, walmart, "Pending", "2026-08-08", 500, "w4@fake.test");
        await ReportAsync(reports, walmart, "Pending", "2026-08-09", 500, "w5@fake.test");

        var snapshot = await Service(database).GetDashboardAsync("all", CancellationToken.None);
        var top = snapshot.TopImpersonatedCompanies;
        var texts = snapshot.Insights.Select(insight => insight.Text).ToList();

        Assert.Equal(["Microsoft", "Amazon"], top.Select(row => row.CompanyName).ToArray());
        Assert.Equal(3, top[0].ApprovedReports);
        Assert.Equal(1, top[1].ApprovedReports);
        Assert.Contains("Highest money requested: Amazon ($80) despite fewer reports.", texts);
    }

    private static DashboardService Service(TempDatabase database) =>
        new(new DashboardRepository(database.ConnectionString));

    private static Task ReportAsync(
        ScamReportRepository reports,
        int companyId,
        string status,
        string date,
        double amount,
        string email) =>
        reports.CreateAsync(
            companyId,
            email,
            "fake.test",
            "Fake check",
            "Email",
            amount,
            12,
            status,
            date,
            "Synthetic report used by the dashboard test.",
            createdByUserId: null,
            CancellationToken.None);
}
