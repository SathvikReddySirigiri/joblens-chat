using JobLens.Infrastructure.Persistence;
using JobLens.Application.Services;
using JobLens.Application.Dtos;
using Xunit;

namespace JobLens.Tests;

public sealed class ServiceRuleTests
{
    [Fact]
    public async Task Report_rules_extract_domain_block_duplicates_and_limit_status_changes()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var companyService = new CompanyService(companies);
        var reportService = new ScamReportService(reports, companies);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var student = await TestActors.CreateStudentAsync(database.ConnectionString);
        var company = await companyService.CreateAsync(SampleCompany(), moderator, CancellationToken.None);
        Assert.True(company.Success);

        var created = await reportService.CreateAsync(SampleReport(company.Id!.Value, "HR@Amaz0n-Careers.NET", "Fake check"), student, CancellationToken.None);
        Assert.True(created.Success);
        var saved = await reports.GetByIdAsync(created.Id!.Value, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("amaz0n-careers.net", saved.Domain);
        Assert.Equal("Pending", saved.Status);
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM-dd"), saved.ReportedDate);
        Assert.Equal(student.UserId, saved.CreatedByUserId);

        var duplicate = await reportService.CreateAsync(SampleReport(company.Id.Value, "hr@amaz0n-careers.net", "Fake check"), student, CancellationToken.None);
        Assert.False(duplicate.Success);

        var approved = await reportService.ApproveAsync(created.Id.Value, moderator, CancellationToken.None);
        Assert.True(approved.Success);
        var again = await reportService.RejectAsync(created.Id.Value, moderator, CancellationToken.None);
        Assert.False(again.Success);

        var blocked = await companyService.DeleteAsync(company.Id.Value, moderator, CancellationToken.None);
        Assert.False(blocked.Success);
        Assert.Equal("This company has 1 related records and cannot be deleted.", blocked.ErrorMessage);
    }

    private static CompanyFormVm SampleCompany() => new()
    {
        Name = "Example Campus Co",
        OfficialDomain = "https://www.Example.com/jobs",
        Industry = "Education",
        HqState = "fl",
    };

    private static ScamReportFormVm SampleReport(int companyId, string email, string scamType) => new()
    {
        CompanyId = companyId,
        RecruiterEmail = email,
        ScamType = scamType,
        Channel = "Email",
        AmountRequestedUsd = 100,
        DomainAgeDays = 12,
        Description = "The recruiter asked the student to deposit a check before the first day.",
    };
}
