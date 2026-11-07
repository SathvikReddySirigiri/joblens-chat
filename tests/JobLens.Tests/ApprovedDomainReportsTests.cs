using JobLens.Domain.Constants;
using JobLens.Infrastructure.Persistence;
using JobLens.Application.Dtos;
using JobLens.Application.Services;
using Xunit;

namespace JobLens.Tests;

public sealed class ApprovedDomainReportsTests
{
    [Fact]
    public async Task GetApprovedByExactDomain_returns_approved_exact_matches_newest_first_max_5()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var recruiters = new RecruiterRepository(database.ConnectionString);
        var companyService = new CompanyService(companies);
        var reportService = new ScamReportService(reports, companies, recruiters);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var student = await TestActors.CreateStudentAsync(database.ConnectionString);
        var company = await companyService.CreateAsync(new CompanyFormVm
        {
            Name = "Domain Exact Co",
            OfficialDomain = "exact.test",
            Industry = "Education",
            HqState = "FL",
        }, moderator, CancellationToken.None);

        for (var i = 1; i <= 6; i++)
        {
            var form = new ScamReportFormVm
            {
                CompanyId = company.Id!.Value,
                RecruiterEmail = $"r{i}@exact-domain.test",
                ScamType = "Fake check",
                Channel = "Email",
                AmountRequestedUsd = i,
                DomainAgeDays = 10,
                ReportedDate = $"2026-0{Math.Min(i, 9)}-01",
                Description = $"Approved community report number {i} with enough detail text.",
            };
            if (i == 6)
            {
                form.ReportedDate = "2026-06-01";
            }

            var created = await reportService.CreateAsync(form, student, CancellationToken.None);
            Assert.True(created.Success);
            if (i < 6)
            {
                Assert.True((await reportService.ApproveAsync(created.Id!.Value, moderator, CancellationToken.None)).Success);
            }
        }

        // Pending report on same domain + approved on a different domain must not appear.
        var other = await reportService.CreateAsync(new ScamReportFormVm
        {
            CompanyId = company.Id!.Value,
            RecruiterEmail = "other@other-domain.test",
            ScamType = "Upfront fee",
            Channel = "Email",
            AmountRequestedUsd = 9,
            DomainAgeDays = 8,
            ReportedDate = "2026-07-01",
            Description = "Different domain should not be returned by exact domain lookup.",
        }, student, CancellationToken.None);
        Assert.True((await reportService.ApproveAsync(other.Id!.Value, moderator, CancellationToken.None)).Success);

        var related = await reports.GetApprovedByExactDomainAsync("exact-domain.test", 5, CancellationToken.None);
        Assert.Equal(5, related.Count);
        Assert.All(related, report =>
        {
            Assert.Equal(ReportStatus.Approved, report.Status);
            Assert.Equal("exact-domain.test", report.Domain, ignoreCase: true);
        });
        Assert.Equal("2026-05-01", related[0].ReportedDate);
        Assert.Equal("2026-04-01", related[1].ReportedDate);
    }
}
