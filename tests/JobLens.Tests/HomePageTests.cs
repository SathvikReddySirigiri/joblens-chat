using System.Globalization;
using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Forms;
using JobLens.Application.Dtos.Pages;
using JobLens.Application.Services;
using JobLens.Domain.Constants;
using JobLens.Infrastructure.Persistence;
using Xunit;

namespace JobLens.Tests;

public sealed class HomePageTests
{
    [Fact]
    public async Task Latest_alerts_are_approved_only_and_newest_first()
    {
        await using var database = await TempDatabase.CreateSeededAsync();
        var home = new HomeService(
            new ScamAnalyticsRepository(database.ConnectionString),
            new JobApplicationRepository(database.ConnectionString),
            new ResumeRepository(database.ConnectionString));

        var page = await home.GetPageAsync(null, CancellationToken.None);
        Assert.Equal(3, page.LatestAlerts.Count);
        Assert.All(page.LatestAlerts, alert => Assert.False(string.IsNullOrWhiteSpace(alert.Domain)));
        Assert.All(page.LatestAlerts, alert => Assert.DoesNotContain("@", alert.Domain));

        for (var i = 1; i < page.LatestAlerts.Count; i++)
        {
            Assert.True(
                string.CompareOrdinal(page.LatestAlerts[i - 1].ReportedDate, page.LatestAlerts[i].ReportedDate) >= 0);
        }

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(database.ConnectionString);
        await connection.OpenAsync();
        foreach (var alert in page.LatestAlerts)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Status FROM ScamReports WHERE ReportId = $id;";
            command.Parameters.AddWithValue("$id", alert.ReportId);
            Assert.Equal(ReportStatus.Approved, Convert.ToString(await command.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public void Red_flag_of_week_is_stable_for_a_given_iso_week()
    {
        var monday = ISOWeek.ToDateTime(2026, 12, DayOfWeek.Monday);
        var sunday = ISOWeek.ToDateTime(2026, 12, DayOfWeek.Sunday);
        var a = ScamPatternCatalog.ForIsoWeek(DateOnly.FromDateTime(monday));
        var b = ScamPatternCatalog.ForIsoWeek(DateOnly.FromDateTime(sunday));
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.ChatQuestion, b.ChatQuestion);

        var nextWeek = ScamPatternCatalog.ForIsoWeek(DateOnly.FromDateTime(ISOWeek.ToDateTime(2026, 13, DayOfWeek.Monday)));
        Assert.NotEqual(a.Name, nextWeek.Name);
    }

    [Fact]
    public async Task Your_week_never_shows_another_users_data()
    {
        await using var database = await TempDatabase.CreateAsync();
        var apps = new JobApplicationRepository(database.ConnectionString);
        var companies = new CompanyRepository(database.ConnectionString);
        var analytics = new ScamAnalyticsRepository(database.ConnectionString);
        var resumes = new ResumeRepository(database.ConnectionString);
        var appService = new ApplicationService(apps, companies, new RecruiterRiskService(analytics));
        var home = new HomeService(analytics, apps, resumes);

        var owner = await TestActors.CreateStudentAsync(database.ConnectionString, "owner@joblens.demo");
        var other = await TestActors.CreateStudentAsync(database.ConnectionString, "other@joblens.demo");

        var form = new JobApplicationFormVm
        {
            CompanyName = "Campus Co",
            RoleTitle = "Intern",
            Source = ApplicationSources.Other,
            Status = ApplicationStatuses.Applied,
            AppliedDate = "2026-01-01",
            NextFollowUpDate = FieldRules.Today(),
            RecruiterEmail = "hr@amaz0n-careers.net",
        };
        Assert.True((await appService.CreateAsync(form, owner, CancellationToken.None)).Success);

        var otherPage = await home.GetPageAsync(other, CancellationToken.None);
        Assert.NotNull(otherPage.StudentWeek);
        Assert.Equal(0, otherPage.StudentWeek!.FollowUpsDueCount);
        Assert.Empty(otherPage.StudentWeek.NextFollowUps);
        Assert.Empty(otherPage.StudentWeek.HighRiskApplications);

        var ownerPage = await home.GetPageAsync(owner, CancellationToken.None);
        Assert.NotNull(ownerPage.StudentWeek);
        Assert.True(ownerPage.StudentWeek!.FollowUpsDueCount >= 1);
        Assert.Contains(ownerPage.StudentWeek.NextFollowUps, item => item.CompanyName == "Campus Co");
    }

    [Fact]
    public void Compact_usd_formats_thousands()
    {
        Assert.Equal("$12.4K", RelativeDate.FormatCompactUsd(12400));
        Assert.Equal("$999", RelativeDate.FormatCompactUsd(999));
    }
}
