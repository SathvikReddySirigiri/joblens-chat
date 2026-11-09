using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Forms;
using JobLens.Application.Services;
using JobLens.Domain.Constants;
using JobLens.Infrastructure.Persistence;
using Xunit;

namespace JobLens.Tests;

public sealed class ApplicationExportAndReportPrefillTests
{
    [Fact]
    public async Task Csv_export_is_owner_only_and_escapes_formula_injection()
    {
        await using var database = await TempDatabase.CreateAsync();
        var apps = new JobApplicationRepository(database.ConnectionString);
        var companies = new CompanyRepository(database.ConnectionString);
        var analytics = new ScamAnalyticsRepository(database.ConnectionString);
        var service = new ApplicationService(apps, companies, new RecruiterRiskService(analytics));
        var owner = await TestActors.CreateStudentAsync(database.ConnectionString, "export-owner@joblens.demo");
        var other = await TestActors.CreateStudentAsync(database.ConnectionString, "export-other@joblens.demo");

        var form = new JobApplicationFormVm
        {
            CompanyName = "=EvilCorp",
            RoleTitle = "+Role",
            Source = ApplicationSources.Other,
            Status = ApplicationStatuses.Saved,
            JobUrl = "https://example.com/job",
        };
        var created = await service.CreateAsync(form, owner, CancellationToken.None);
        Assert.True(created.Success, created.ErrorMessage);

        // Bypass form URL rules to prove export escapes formula-like JobUrl values.
        Assert.True(await apps.UpdateAsync(
            created.Id!.Value,
            owner.UserId,
            "=EvilCorp",
            null,
            "+Role",
            ApplicationSources.Other,
            "@https://evil.example/job",
            null,
            ApplicationStatuses.Saved,
            null,
            null,
            null,
            null,
            null,
            FieldRules.Today(),
            CancellationToken.None));

        var ownerCsv = await service.ExportCsvAsync(owner, null, null, CancellationToken.None);
        Assert.Contains("Company,Role,Status,Source,AppliedDate,NextFollowUp,RiskVerdict,JobUrl", ownerCsv);
        Assert.Contains("'=EvilCorp", ownerCsv);
        Assert.Contains("'+Role", ownerCsv);
        Assert.Contains("'@https://evil.example/job", ownerCsv);

        var otherCsv = await service.ExportCsvAsync(other, null, null, CancellationToken.None);
        Assert.Single(otherCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.DoesNotContain("EvilCorp", otherCsv);
    }

    [Fact]
    public void Csv_escape_prefixes_formula_starters()
    {
        Assert.Equal("'=SUM(1)", CsvExport.Escape("=SUM(1)"));
        Assert.Equal("'+1", CsvExport.Escape("+1"));
        Assert.Equal("'-1", CsvExport.Escape("-1"));
        Assert.Equal("'@x", CsvExport.Escape("@x"));
        Assert.Equal("\"a,b\"", CsvExport.Escape("a,b"));
    }

    [Fact]
    public async Task Report_prefill_uses_email_and_matched_company_from_high_risk_check()
    {
        await using var database = await TempDatabase.CreateSeededAsync();
        var analytics = new ScamAnalyticsRepository(database.ConnectionString);
        var risk = new RecruiterRiskService(analytics);
        var result = await risk.CheckAsync("hr@amaz0n-careers.net", "Amazon", CancellationToken.None);

        Assert.Equal(Verdicts.High, result.Verdict);
        Assert.Equal("amaz0n-careers.net", result.Domain);
        Assert.False(string.IsNullOrWhiteSpace(result.MatchedCompany));
        Assert.NotNull(result.MatchedCompanyId);

        var form = new ScamReportFormVm
        {
            RecruiterEmail = result.Email,
            CompanyId = result.MatchedCompanyId ?? 0,
        };
        Assert.Equal("hr@amaz0n-careers.net", form.RecruiterEmail);
        Assert.True(form.CompanyId > 0);
        Assert.Equal(result.Domain, FieldRules.DomainOfEmail(form.RecruiterEmail));
    }
}
