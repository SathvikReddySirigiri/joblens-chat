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
        var recruiters = new RecruiterRepository(database.ConnectionString);
        var companyService = new CompanyService(companies);
        var reportService = new ScamReportService(reports, companies, recruiters);
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
        Assert.StartsWith("This company has ", blocked.ErrorMessage);
        Assert.EndsWith(" related records and cannot be deleted.", blocked.ErrorMessage);

        var recruiter = await recruiters.GetByEmailAsync("hr@amaz0n-careers.net", CancellationToken.None);
        Assert.NotNull(recruiter);
        Assert.Equal(company.Id.Value, recruiter.CompanyId);
    }

    [Fact]
    public async Task Paid_posting_requires_a_rate_and_unpaid_rejects_a_positive_rate()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var postings = new JobPostingRepository(database.ConnectionString);
        var companyService = new CompanyService(companies);
        var postingService = new JobPostingService(postings, companies);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var company = await companyService.CreateAsync(PlainCompany(), moderator, CancellationToken.None);
        Assert.True(company.Success);

        var missingRate = await postingService.CreateAsync(Posting(company.Id!.Value, paid: true, pay: null), moderator, CancellationToken.None);
        Assert.False(missingRate.Success);

        var paid = await postingService.CreateAsync(Posting(company.Id.Value, paid: true, pay: 22), moderator, CancellationToken.None);
        Assert.True(paid.Success);
        var savedPaid = await postings.GetByIdAsync(paid.Id!.Value, CancellationToken.None);
        Assert.Equal(22, savedPaid!.PayRatePerHour);

        var unpaidWithRate = await postingService.CreateAsync(Posting(company.Id.Value, paid: false, pay: 18), moderator, CancellationToken.None);
        Assert.False(unpaidWithRate.Success);
        Assert.Equal("An unpaid posting cannot include a pay rate. Leave it empty or zero.", unpaidWithRate.ErrorMessage);

        var unpaid = await postingService.CreateAsync(Posting(company.Id.Value, paid: false, pay: 0), moderator, CancellationToken.None);
        Assert.True(unpaid.Success);
        var savedUnpaid = await postings.GetByIdAsync(unpaid.Id!.Value, CancellationToken.None);
        Assert.Null(savedUnpaid!.PayRatePerHour);
    }

    [Fact]
    public async Task Company_domain_is_normalized_on_save()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var service = new CompanyService(companies);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var created = await service.CreateAsync(new CompanyFormVm
        {
            Name = "Example Campus Co",
            OfficialDomain = "https://www.Example.com/jobs",
            Industry = "Education",
            HqState = "fl",
        }, moderator, CancellationToken.None);

        Assert.True(created.Success);
        var saved = await companies.GetByIdAsync(created.Id!.Value, CancellationToken.None);
        Assert.Equal("example.com", saved!.OfficialDomain);
        Assert.Equal("FL", saved.HqState);
    }

    [Fact]
    public async Task Scam_report_can_be_approved_only_while_pending()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var recruiters = new RecruiterRepository(database.ConnectionString);
        var companyService = new CompanyService(companies);
        var reportService = new ScamReportService(reports, companies, recruiters);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var student = await TestActors.CreateStudentAsync(database.ConnectionString);
        var company = await companyService.CreateAsync(PlainCompany(), moderator, CancellationToken.None);
        var created = await reportService.CreateAsync(SampleReport(company.Id!.Value, "hr@example.com", "Fake check"), student, CancellationToken.None);
        Assert.Equal("Pending", (await reports.GetByIdAsync(created.Id!.Value, CancellationToken.None))!.Status);

        var approved = await reportService.ApproveAsync(created.Id.Value, moderator, CancellationToken.None);
        Assert.True(approved.Success);
        var second = await reportService.ApproveAsync(created.Id.Value, moderator, CancellationToken.None);
        Assert.False(second.Success);
        Assert.Equal("Only pending reports can be approved or rejected.", second.ErrorMessage);
        Assert.Equal("Approved", (await reports.GetByIdAsync(created.Id.Value, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task Company_delete_is_blocked_when_related_records_exist()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var recruiters = new RecruiterRepository(database.ConnectionString);
        var companyService = new CompanyService(companies);
        var reportService = new ScamReportService(reports, companies, recruiters);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var student = await TestActors.CreateStudentAsync(database.ConnectionString);
        var company = await companyService.CreateAsync(PlainCompany(), moderator, CancellationToken.None);
        await reportService.CreateAsync(SampleReport(company.Id!.Value, "hr@example.com", "Fake check"), student, CancellationToken.None);

        var blocked = await companyService.DeleteAsync(company.Id.Value, moderator, CancellationToken.None);
        Assert.False(blocked.Success);
        Assert.StartsWith("This company has ", blocked.ErrorMessage);
        Assert.EndsWith(" related records and cannot be deleted.", blocked.ErrorMessage);
        Assert.NotNull(await companies.GetByIdAsync(company.Id.Value, CancellationToken.None));
    }

    private static CompanyFormVm SampleCompany() => new()
    {
        Name = "Example Campus Co",
        OfficialDomain = "https://www.Example.com/jobs",
        Industry = "Education",
        HqState = "fl",
    };

    private static CompanyFormVm PlainCompany() => new()
    {
        Name = "Example Campus Co",
        OfficialDomain = "example.com",
        Industry = "Education",
        HqState = "FL",
    };

    private static JobPostingFormVm Posting(int companyId, bool paid, double? pay) => new()
    {
        CompanyId = companyId,
        Title = "Data Analyst Intern",
        Location = "Tampa",
        State = "FL",
        IsRemote = false,
        IsPaid = paid,
        PayRatePerHour = pay,
        Description = "A paid campus internship supporting the analytics team this summer.",
        PostedDate = "2026-06-01",
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
