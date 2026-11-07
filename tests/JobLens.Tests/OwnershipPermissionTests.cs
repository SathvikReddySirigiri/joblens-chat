using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Infrastructure.Persistence;
using Xunit;

namespace JobLens.Tests;

public sealed class OwnershipPermissionTests
{
    [Fact]
    public async Task Owner_can_edit_own_pending_report_but_not_after_approval()
    {
        await using var database = await TempDatabase.CreateAsync();
        var (service, companyId, moderator, owner, otherStudent) = await SetupAsync(database);

        var created = await service.CreateAsync(Report(companyId, "owner@scam.test"), owner, CancellationToken.None);
        Assert.True(created.Success);

        var edit = await service.UpdateAsync(Report(companyId, "owner@scam.test", created.Id!.Value), owner, CancellationToken.None);
        Assert.True(edit.Success);

        var blockedOther = await service.UpdateAsync(Report(companyId, "owner@scam.test", created.Id.Value), otherStudent, CancellationToken.None);
        Assert.False(blockedOther.Success);
        Assert.Equal("You can only edit your own pending reports.", blockedOther.ErrorMessage);

        Assert.True((await service.ApproveAsync(created.Id.Value, moderator, CancellationToken.None)).Success);
        var blockedAfterApprove = await service.UpdateAsync(Report(companyId, "owner@scam.test", created.Id.Value), owner, CancellationToken.None);
        Assert.False(blockedAfterApprove.Success);
        Assert.Equal("You can only edit your own reports while they are still Pending.", blockedAfterApprove.ErrorMessage);
    }

    [Fact]
    public async Task Moderator_can_approve_and_student_cannot()
    {
        await using var database = await TempDatabase.CreateAsync();
        var (service, companyId, moderator, owner, _) = await SetupAsync(database);
        var created = await service.CreateAsync(Report(companyId, "approve@scam.test"), owner, CancellationToken.None);

        var studentAttempt = await service.ApproveAsync(created.Id!.Value, owner, CancellationToken.None);
        Assert.False(studentAttempt.Success);
        Assert.Equal("Only moderators can approve or reject reports.", studentAttempt.ErrorMessage);

        var approved = await service.ApproveAsync(created.Id.Value, moderator, CancellationToken.None);
        Assert.True(approved.Success);
    }

    [Fact]
    public async Task List_visibility_filters_by_role()
    {
        await using var database = await TempDatabase.CreateAsync();
        var (service, companyId, moderator, owner, otherStudent) = await SetupAsync(database);

        var mine = await service.CreateAsync(Report(companyId, "mine@scam.test", scamType: "Fake check"), owner, CancellationToken.None);
        var theirs = await service.CreateAsync(Report(companyId, "theirs@scam.test", scamType: "Upfront fee"), otherStudent, CancellationToken.None);
        Assert.True(mine.Success);
        Assert.True(theirs.Success);
        Assert.True((await service.ApproveAsync(theirs.Id!.Value, moderator, CancellationToken.None)).Success);

        var visitor = await service.BrowseAsync(null, null, null, null, null, "date", true, 1, viewer: null, CancellationToken.None);
        Assert.All(visitor.Items, report => Assert.Equal("Approved", report.Status));
        Assert.Contains(visitor.Items, report => report.ReportId == theirs.Id);
        Assert.DoesNotContain(visitor.Items, report => report.ReportId == mine.Id);

        var studentView = await service.BrowseAsync(null, null, null, null, null, "date", true, 1, owner, CancellationToken.None);
        Assert.Contains(studentView.Items, report => report.ReportId == mine.Id);
        Assert.Contains(studentView.Items, report => report.ReportId == theirs.Id);

        var moderatorView = await service.BrowseAsync(null, null, null, null, null, "date", true, 1, moderator, CancellationToken.None);
        Assert.Equal(2, moderatorView.Total);
    }

    [Fact]
    public async Task Student_cannot_manage_companies()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var service = new CompanyService(companies);
        var student = await TestActors.CreateStudentAsync(database.ConnectionString);

        var blocked = await service.CreateAsync(new CompanyFormVm
        {
            Name = "Blocked Co",
            OfficialDomain = "blocked.test",
            Industry = "Education",
            HqState = "FL",
        }, student, CancellationToken.None);

        Assert.False(blocked.Success);
        Assert.Equal("Only moderators can create, edit, or delete companies.", blocked.ErrorMessage);
    }

    private static async Task<(ScamReportService Service, int CompanyId, ActingUser Moderator, ActingUser Owner, ActingUser OtherStudent)> SetupAsync(
        TempDatabase database)
    {
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var recruiters = new RecruiterRepository(database.ConnectionString);
        var companyService = new CompanyService(companies);
        var reportService = new ScamReportService(reports, companies, recruiters);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var owner = await TestActors.CreateStudentAsync(database.ConnectionString, "owner@test.local");
        var otherStudent = await TestActors.CreateStudentAsync(database.ConnectionString, "other@test.local");
        var company = await companyService.CreateAsync(new CompanyFormVm
        {
            Name = "Campus Target",
            OfficialDomain = "campus.test",
            Industry = "Education",
            HqState = "FL",
        }, moderator, CancellationToken.None);
        Assert.True(company.Success);
        return (reportService, company.Id!.Value, moderator, owner, otherStudent);
    }

    private static ScamReportFormVm Report(
        int companyId,
        string email,
        int reportId = 0,
        string scamType = "Fake check") => new()
    {
        ReportId = reportId,
        CompanyId = companyId,
        RecruiterEmail = email,
        ScamType = scamType,
        Channel = "Email",
        AmountRequestedUsd = 100,
        DomainAgeDays = 12,
        Description = "The recruiter asked the student to deposit a check before the first day.",
        ReportedDate = "2026-03-01",
    };
}
