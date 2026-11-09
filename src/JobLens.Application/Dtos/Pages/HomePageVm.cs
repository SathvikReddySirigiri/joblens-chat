namespace JobLens.Application.Dtos.Pages;

public sealed class HomePageVm
{
    public HomeImpactStats Stats { get; init; } = new(0, 0, 0, "$0", 0);

    public IReadOnlyList<HomeScamAlert> LatestAlerts { get; init; } = [];

    public HomeRedFlag RedFlagOfWeek { get; init; } = ScamPatternCatalog.ForIsoWeek(DateOnly.FromDateTime(DateTime.UtcNow));

    public HomeStudentWeek? StudentWeek { get; init; }

    public HomeModeratorWeek? ModeratorWeek { get; init; }
}

public sealed record HomeImpactStats(
    int ApprovedReports,
    int CompaniesImpersonated,
    double AmountRequestedUsd,
    string AmountFormatted,
    int RecruiterChecks);

public sealed record HomeScamAlert(
    int ReportId,
    string CompanyName,
    string Domain,
    string ScamType,
    double AmountRequestedUsd,
    string ReportedDate,
    string RelativeDate);

public sealed record HomeRedFlag(
    string Name,
    string HowItWorks,
    IReadOnlyList<string> RedFlags,
    string WhatToDo,
    string ChatQuestion);

public sealed record HomeStudentWeek(
    int FollowUpsDueCount,
    IReadOnlyList<HomeFollowUpItem> NextFollowUps,
    IReadOnlyList<HomeHighRiskApplication> HighRiskApplications,
    int? ResumePrivacyIssueCount,
    bool HasResume);

public sealed record HomeFollowUpItem(
    int ApplicationId,
    string CompanyName,
    string RoleTitle,
    string NextFollowUpDate);

public sealed record HomeHighRiskApplication(
    int ApplicationId,
    string CompanyName,
    string RoleTitle,
    string RiskVerdict);

public sealed record HomeModeratorWeek(
    int PendingReports,
    int NewReportsThisWeek);

public sealed record RecentCheckItem(
    int CheckId,
    string Domain,
    string Verdict,
    string CreatedAt);
