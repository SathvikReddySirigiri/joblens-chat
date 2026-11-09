using System.Globalization;
using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Pages;
// ActingUser lives in Dtos.
using JobLens.Application.Interfaces;
using JobLens.Domain.Constants;

namespace JobLens.Application.Services;

public sealed class HomeService(
    IScamAnalyticsRepository analytics,
    IJobApplicationRepository applications,
    IResumeRepository resumes)
{
    public Task<DataCounts> GetCountsAsync(CancellationToken cancellationToken) =>
        analytics.GetCountsAsync(cancellationToken);

    public async Task<HomePageVm> GetPageAsync(ActingUser? actor, CancellationToken cancellationToken)
    {
        var today = FieldRules.Today();
        var (approved, companies, amount, checks) = await analytics.GetHomeImpactStatsAsync(cancellationToken);
        var alerts = await analytics.GetLatestApprovedReportsAsync(3, cancellationToken);
        var redFlag = ScamPatternCatalog.ForIsoWeek(ParseToday(today));

        HomeStudentWeek? studentWeek = null;
        HomeModeratorWeek? moderatorWeek = null;
        if (actor?.IsModerator == true)
        {
            var weekStart = StartOfIsoWeek(ParseToday(today)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            moderatorWeek = new HomeModeratorWeek(
                await analytics.CountPendingReportsAsync(cancellationToken),
                await analytics.CountReportsSinceAsync(weekStart, cancellationToken));
        }
        else if (actor is { UserId: > 0 })
        {
            studentWeek = await BuildStudentWeekAsync(actor.UserId, today, cancellationToken);
        }

        return new HomePageVm
        {
            Stats = new HomeImpactStats(
                approved,
                companies,
                amount,
                RelativeDate.FormatCompactUsd(amount),
                checks),
            LatestAlerts = alerts
                .Select(report => new HomeScamAlert(
                    report.ReportId,
                    report.CompanyName,
                    report.Domain,
                    report.ScamType,
                    report.AmountRequestedUsd,
                    report.ReportedDate,
                    RelativeDate.Format(report.ReportedDate, today)))
                .ToList(),
            RedFlagOfWeek = redFlag,
            StudentWeek = studentWeek,
            ModeratorWeek = moderatorWeek,
        };
    }

    private async Task<HomeStudentWeek> BuildStudentWeekAsync(int userId, string today, CancellationToken cancellationToken)
    {
        // Owner-scoped export list doubles as the "your week" source (still capped in the repository).
        var export = await applications.ListForExportAsync(userId, null, null, cancellationToken);
        var followUps = export
            .Where(row => ApplicationService.IsFollowUpDue(row, today))
            .OrderBy(row => row.NextFollowUpDate)
            .ThenBy(row => row.ApplicationId)
            .ToList();
        var highRisk = export
            .Where(row => string.Equals(row.RiskVerdict, ApplicationRiskVerdicts.High, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(row => row.UpdatedAt)
            .Take(5)
            .Select(row => new HomeHighRiskApplication(row.ApplicationId, row.CompanyName, row.RoleTitle, row.RiskVerdict ?? ApplicationRiskVerdicts.High))
            .ToList();
        var resume = await resumes.GetByUserIdAsync(userId, cancellationToken);

        return new HomeStudentWeek(
            followUps.Count,
            followUps
                .Take(3)
                .Select(row => new HomeFollowUpItem(
                    row.ApplicationId,
                    row.CompanyName,
                    row.RoleTitle,
                    row.NextFollowUpDate ?? ""))
                .ToList(),
            highRisk,
            resume?.PrivacyIssueCount,
            resume is not null);
    }

    private static DateOnly ParseToday(string today) =>
        DateOnly.TryParse(today, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateOnly.FromDateTime(DateTime.UtcNow);

    private static DateOnly StartOfIsoWeek(DateOnly date)
    {
        // ISO weeks start Monday.
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }
}
