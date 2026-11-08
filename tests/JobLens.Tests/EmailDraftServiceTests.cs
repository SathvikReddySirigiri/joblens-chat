using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Application.Services;
using JobLens.Domain.Constants;
using JobLens.Infrastructure.Persistence;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class EmailDraftServiceTests
{
    [Fact]
    public async Task High_risk_recruiter_forces_verify_type_and_warning()
    {
        await using var database = await TempDatabase.CreateSeededAsync();
        var service = CreateService(database.ConnectionString);
        var actor = ActingUser.Student(await UserIdAsync(database.ConnectionString, "student1@joblens.demo"));

        var result = await service.DraftAsync(
            new EmailDraftRequest(
                EmailDraftTypes.FollowUpAfterApplying,
                ApplicationId: null,
                RecruiterName: "Alex",
                RecruiterEmail: "hr@amaz0n-careers.net",
                CompanyName: "Amazon",
                RoleTitle: "Backend Intern",
                StudentName: "Demo Student",
                Tone: EmailDraftTones.Professional,
                ExtraNotes: null),
            actor,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Draft);
        Assert.Equal(EmailDraftTypes.VerifyRecruiter, result.Draft.EffectiveType);
        Assert.Contains(EmailDraftService.HighRiskWarning, result.Draft.Warnings);
        Assert.Contains("Amazon", result.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("Backend Intern", result.Draft.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pii_in_notes_is_redacted_from_output()
    {
        await using var database = await TempDatabase.CreateAsync();
        var service = CreateService(database.ConnectionString);
        var actor = await TestActors.CreateStudentAsync(database.ConnectionString);

        var result = await service.DraftAsync(
            new EmailDraftRequest(
                EmailDraftTypes.FollowUpAfterApplying,
                null,
                null,
                null,
                "Campus Co",
                "Analyst Intern",
                "Jordan Lee",
                EmailDraftTones.Brief,
                "My SSN is 123-45-6789 and password=Secret123"),
            actor,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain("123-45-6789", result.Draft!.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret123", result.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("***", result.Draft.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_template_for_each_type_contains_company_and_role()
    {
        foreach (var type in EmailDraftTypes.All)
        {
            var (subject, body) = EmailDraftService.BuildTemplate(
                type,
                EmailDraftTones.Professional,
                "Acme Robotics",
                "Data Intern",
                "Sam Student",
                "Pat Recruiter",
                null);
            Assert.Contains("Acme Robotics", subject + body, StringComparison.Ordinal);
            Assert.Contains("Data Intern", subject + body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Mailto_encoding_escapes_subject_and_body()
    {
        var url = EmailDraftService.BuildMailto(
            "recruiter@example.com",
            "Hello & welcome",
            "Line 1\nLine 2?");
        Assert.NotNull(url);
        Assert.StartsWith("mailto:recruiter@example.com?", url);
        Assert.Contains("subject=Hello%20%26%20welcome", url, StringComparison.Ordinal);
        Assert.Contains("body=Line%201%0ALine%202%3F", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Another_users_application_id_returns_not_found()
    {
        await using var database = await TempDatabase.CreateAsync();
        var apps = new JobApplicationRepository(database.ConnectionString);
        var companies = new CompanyRepository(database.ConnectionString);
        var analytics = new ScamAnalyticsRepository(database.ConnectionString);
        var applicationService = new ApplicationService(apps, companies, new RecruiterRiskService(analytics));
        var owner = await TestActors.CreateStudentAsync(database.ConnectionString, "owner-draft@test.local");
        var other = await TestActors.CreateStudentAsync(database.ConnectionString, "other-draft@test.local");
        var created = await applicationService.CreateAsync(
            new JobApplicationFormVm
            {
                CompanyName = "Campus Co",
                RoleTitle = "Intern",
                Source = ApplicationSources.Other,
                Status = ApplicationStatuses.Saved,
            },
            owner,
            CancellationToken.None);
        Assert.True(created.Success);

        var service = CreateService(database.ConnectionString);
        var result = await service.DraftAsync(
            new EmailDraftRequest(
                EmailDraftTypes.ThankYouAfterInterview,
                created.Id,
                null,
                null,
                "Campus Co",
                "Intern",
                "Other User",
                EmailDraftTones.Professional,
                null),
            other,
            CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.False(result.Success);
        Assert.Null(result.Draft);
    }

    private static EmailDraftService CreateService(string connectionString)
    {
        var apps = new JobApplicationRepository(connectionString);
        var analytics = new ScamAnalyticsRepository(connectionString);
        return new EmailDraftService(
            new StubAiFactory(),
            new PiiRedactor(),
            new RecruiterRiskService(analytics),
            apps,
            NullLogger<EmailDraftService>.Instance);
    }

    private static async Task<int> UserIdAsync(string connectionString, string email)
    {
        await using var connection = await SqliteConnections.OpenAsync(connectionString, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserId FROM Users WHERE Email = $email;";
        command.Parameters.AddWithValue("$email", email);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class StubAiFactory : IAiClientFactory
    {
        public bool IsConfigured => false;

        public string Label => "Demo";

        public IChatClient CreateClient(bool useFunctionInvocation) =>
            throw new NotSupportedException();
    }
}
