using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Infrastructure.Persistence;
using Xunit;

namespace JobLens.Tests;

public sealed class ScamReportValidationTests
{
    [Fact]
    public async Task Create_rejects_future_date_huge_amount_and_huge_domain_age()
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
            Name = "Validation Co",
            OfficialDomain = "validation.test",
            Industry = "Education",
            HqState = "FL",
        }, moderator, CancellationToken.None);

        var future = Sample(company.Id!.Value);
        future.ReportedDate = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd");
        Assert.False((await reportService.CreateAsync(future, student, CancellationToken.None)).Success);

        var amount = Sample(company.Id.Value);
        amount.AmountRequestedUsd = 1_000_001;
        Assert.False((await reportService.CreateAsync(amount, student, CancellationToken.None)).Success);

        var age = Sample(company.Id.Value);
        age.DomainAgeDays = 20_001;
        Assert.False((await reportService.CreateAsync(age, student, CancellationToken.None)).Success);
    }

    [Fact]
    public async Task Create_adds_missing_recruiter_for_impersonated_company()
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
            Name = "Recruiter Seed Co",
            OfficialDomain = "seed.test",
            Industry = "Education",
            HqState = "NY",
        }, moderator, CancellationToken.None);

        Assert.Null(await recruiters.GetByEmailAsync("spoof@seed-careers.test", CancellationToken.None));
        var created = await reportService.CreateAsync(Sample(company.Id!.Value), student, CancellationToken.None);
        Assert.True(created.Success);
        var recruiter = await recruiters.GetByEmailAsync("spoof@seed-careers.test", CancellationToken.None);
        Assert.NotNull(recruiter);
        Assert.Equal(company.Id.Value, recruiter.CompanyId);
        Assert.Equal("seed-careers.test", recruiter.Domain);
    }

    private static ScamReportFormVm Sample(int companyId) => new()
    {
        CompanyId = companyId,
        RecruiterEmail = "spoof@seed-careers.test",
        ScamType = "Fake check",
        Channel = "Email",
        AmountRequestedUsd = 250,
        DomainAgeDays = 14,
        Description = "The recruiter asked for a check deposit before the first day of work.",
    };
}
