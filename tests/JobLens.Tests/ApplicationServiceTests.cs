using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Forms;
using JobLens.Application.Services;
using JobLens.Domain.Constants;
using JobLens.Infrastructure.Persistence;
using Xunit;

namespace JobLens.Tests;

public sealed class ApplicationServiceTests
{
    [Fact]
    public async Task Owner_isolation_blocks_other_users_from_read_edit_delete()
    {
        await using var database = await TempDatabase.CreateAsync();
        var (service, owner, other) = await SetupAsync(database);

        var created = await service.CreateAsync(SampleForm("Campus Co", "Analyst Intern"), owner, CancellationToken.None);
        Assert.True(created.Success);

        Assert.Null(await service.GetByIdAsync(created.Id!.Value, other, CancellationToken.None));
        Assert.Equal(0, (await service.BrowseAsync(other, null, null, 1, CancellationToken.None)).Total);

        var edit = await service.UpdateAsync(SampleForm("Campus Co", "Analyst Intern", created.Id.Value), other, CancellationToken.None);
        Assert.False(edit.Success);
        Assert.Equal("Application not found.", edit.ErrorMessage);

        var delete = await service.DeleteAsync(created.Id.Value, other, CancellationToken.None);
        Assert.False(delete.Success);
        Assert.Equal("Application not found.", delete.ErrorMessage);

        Assert.NotNull(await service.GetByIdAsync(created.Id.Value, owner, CancellationToken.None));
    }

    [Fact]
    public async Task Moderator_cannot_list_other_users_applications()
    {
        await using var database = await TempDatabase.CreateAsync();
        var (service, owner, _) = await SetupAsync(database);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);

        Assert.True((await service.CreateAsync(SampleForm("Campus Co", "Analyst Intern"), owner, CancellationToken.None)).Success);
        Assert.Equal(1, (await service.BrowseAsync(owner, null, null, 1, CancellationToken.None)).Total);
        Assert.Equal(0, (await service.BrowseAsync(moderator, null, null, 1, CancellationToken.None)).Total);
        Assert.Equal(1, await service.CountAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Validation_rejects_bad_url_future_applied_date_and_follow_up_before_applied()
    {
        await using var database = await TempDatabase.CreateAsync();
        var (service, owner, _) = await SetupAsync(database);

        var badUrl = SampleForm("Campus Co", "Intern");
        badUrl.JobUrl = "ftp://example.com";
        Assert.False((await service.CreateAsync(badUrl, owner, CancellationToken.None)).Success);

        var future = SampleForm("Campus Co", "Intern");
        future.AppliedDate = "2099-01-01";
        Assert.False((await service.CreateAsync(future, owner, CancellationToken.None)).Success);

        var followUp = SampleForm("Campus Co", "Intern");
        followUp.AppliedDate = "2026-10-10";
        followUp.NextFollowUpDate = "2026-10-01";
        Assert.False((await service.CreateAsync(followUp, owner, CancellationToken.None)).Success);
    }

    [Fact]
    public async Task Recruiter_email_from_seeded_scam_domain_sets_high_risk_verdict()
    {
        await using var database = await TempDatabase.CreateSeededAsync();
        var apps = new JobApplicationRepository(database.ConnectionString);
        var companies = new CompanyRepository(database.ConnectionString);
        var analytics = new ScamAnalyticsRepository(database.ConnectionString);
        var service = new ApplicationService(apps, companies, new RecruiterRiskService(analytics));
        var owner = ActingUser.Student(await UserIdAsync(database.ConnectionString, "student1@joblens.demo"));

        var form = SampleForm("Amazon", "Backend Intern");
        form.RecruiterEmail = "hr@amaz0n-careers.net";
        form.Status = ApplicationStatuses.Applied;
        form.AppliedDate = "2026-10-01";
        var created = await service.CreateAsync(form, owner, CancellationToken.None);
        Assert.True(created.Success);
        var saved = await service.GetByIdAsync(created.Id!.Value, owner, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(ApplicationRiskVerdicts.High, saved.RiskVerdict);
        Assert.NotNull(saved.CompanyId);
    }

    [Fact]
    public async Task Follow_up_due_when_date_is_today_or_earlier_and_status_open()
    {
        await using var database = await TempDatabase.CreateAsync();
        var (service, owner, _) = await SetupAsync(database);
        var today = FieldRules.Today();

        var dueForm = SampleForm("Campus Co", "Intern");
        dueForm.Status = ApplicationStatuses.Applied;
        dueForm.AppliedDate = "2026-01-01";
        dueForm.NextFollowUpDate = today;
        var due = await service.CreateAsync(dueForm, owner, CancellationToken.None);
        Assert.True(due.Success);
        Assert.True(ApplicationService.IsFollowUpDue((await service.GetByIdAsync(due.Id!.Value, owner, CancellationToken.None))!));

        var closedForm = SampleForm("Campus Co", "Closed Role");
        closedForm.Status = ApplicationStatuses.Offer;
        closedForm.AppliedDate = "2026-01-01";
        closedForm.NextFollowUpDate = today;
        var closed = await service.CreateAsync(closedForm, owner, CancellationToken.None);
        Assert.True(closed.Success);
        var offer = await service.GetByIdAsync(closed.Id!.Value, owner, CancellationToken.None);
        Assert.False(ApplicationService.IsFollowUpDue(offer!));
        Assert.Equal(today, offer!.ClosedDate);
    }

    private static async Task<(ApplicationService Service, ActingUser Owner, ActingUser Other)> SetupAsync(TempDatabase database)
    {
        var apps = new JobApplicationRepository(database.ConnectionString);
        var companies = new CompanyRepository(database.ConnectionString);
        var analytics = new ScamAnalyticsRepository(database.ConnectionString);
        var service = new ApplicationService(apps, companies, new RecruiterRiskService(analytics));
        var owner = await TestActors.CreateStudentAsync(database.ConnectionString, "owner-apps@test.local");
        var other = await TestActors.CreateStudentAsync(database.ConnectionString, "other-apps@test.local");
        return (service, owner, other);
    }

    private static async Task<int> UserIdAsync(string connectionString, string email)
    {
        await using var connection = await SqliteConnections.OpenAsync(connectionString, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserId FROM Users WHERE Email = $email;";
        command.Parameters.AddWithValue("$email", email);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static JobApplicationFormVm SampleForm(string company, string role, int id = 0) => new()
    {
        ApplicationId = id,
        CompanyName = company,
        RoleTitle = role,
        Source = ApplicationSources.Other,
        Status = ApplicationStatuses.Saved,
    };
}
