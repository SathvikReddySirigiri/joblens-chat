using System.Globalization;
using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Dashboard;
using JobLens.Application.Interfaces;

namespace JobLens.Application.Services;

public sealed class DashboardService(IDashboardRepository repository)
{
    public Task<DashboardData> GetDashboardAsync(string? range, CancellationToken cancellationToken) =>
        GetAsync(range, cancellationToken);

    public async Task<DashboardData> GetAsync(string? range, CancellationToken cancellationToken)
    {
        var latest = await repository.GetLatestReportDateAsync(cancellationToken);
        var (window, label) = ResolveRange(range, latest);
        var checkMonth = await ResolveChecksMonthAsync(cancellationToken);
        var (kpis, highThisMonth) = await repository.GetKpisAsync(window, checkMonth, cancellationToken);
        var months = await repository.GetReportsPerMonthAsync(window, cancellationToken);
        var types = await repository.GetReportsByScamTypeAsync(window, cancellationToken);
        var statuses = OrderStatuses(await repository.GetReportsByStatusAsync(window, cancellationToken));
        var topCandidates = await repository.GetTopImpersonatedAsync(window, 20, cancellationToken);
        var top = topCandidates.Take(7).ToList();
        var moneyLeader = topCandidates
            .OrderByDescending(row => row.TotalAmountRequestedUsd)
            .ThenBy(row => row.CompanyName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var checks = await repository.GetChecksByVerdictPerMonthAsync(window, cancellationToken);
        var channels = await repository.GetReportsByChannelAsync(window, cancellationToken);
        var activity = await repository.GetCompanyActivityAsync(window, 10, cancellationToken);
        var insights = BuildInsights(kpis, months, top, moneyLeader, highThisMonth);
        return new DashboardData(kpis, months, types, statuses, top, checks, channels, activity, insights, latest, label);
    }

    public static (DateRange Range, string Label) ResolveRange(string? range, string? latestReportedDate)
    {
        var label = string.IsNullOrWhiteSpace(range) ? "all" : range.Trim().ToLowerInvariant();
        if (label is not ("3m" or "6m")
            || string.IsNullOrWhiteSpace(latestReportedDate)
            || !DateOnly.TryParseExact(latestReportedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var latest))
        {
            return (DateRange.All, "all");
        }

        var monthsBack = label == "3m" ? 3 : 6;
        var from = latest.AddMonths(-monthsBack).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (new DateRange(from, latest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), label);
    }

    // Kept for existing tests that call Window(...).From / .To / .Label
    internal static (string? From, string? To, string Label) Window(string? range, string? latestReportedDate)
    {
        var (resolved, label) = ResolveRange(range, latestReportedDate);
        return (resolved.FromDate, resolved.ToDate, label);
    }

    internal static IReadOnlyList<DashboardInsight> BuildInsights(
        DashboardKpis kpis,
        IReadOnlyList<MonthCount> months,
        IReadOnlyList<CompanyImpersonation> top,
        CompanyImpersonation? moneyLeader,
        int highChecksThisMonth)
    {
        var insights = new List<DashboardInsight>();
        var july = months.FirstOrDefault(month => month.Month.EndsWith("-07", StringComparison.Ordinal));
        var october = months.FirstOrDefault(month => month.Month.EndsWith("-10", StringComparison.Ordinal));
        if (july is not null && october is not null && july.Total > 0)
        {
            var change = (october.Total - july.Total) * 100.0 / july.Total;
            var verb = change >= 0 ? "grew" : "fell";
            insights.Add(new DashboardInsight(
                $"Reports {verb} {Math.Abs(change).ToString("0.#", CultureInfo.InvariantCulture)}% from July to October."));
        }
        else if (months.Count >= 2 && months[0].Total > 0)
        {
            var change = (months[^1].Total - months[0].Total) * 100.0 / months[0].Total;
            var verb = change >= 0 ? "grew" : "fell";
            insights.Add(new DashboardInsight(
                $"Reports {verb} {Math.Abs(change).ToString("0.#", CultureInfo.InvariantCulture)}% from {MonthName(months[0].Month)} to {MonthName(months[^1].Month)}."));
        }

        if (top.Count > 0)
        {
            var leader = top[0];
            insights.Add(new DashboardInsight(
                $"{leader.CompanyName} is the most impersonated company ({leader.ApprovedReports} approved reports, {leader.TotalAmountRequestedUsd.ToString("$#,##0.00", CultureInfo.InvariantCulture)} requested)."));
        }

        if (top.Count > 0 && moneyLeader is not null && moneyLeader.ApprovedReports < top[0].ApprovedReports)
        {
            insights.Add(new DashboardInsight(
                $"Highest money requested: {moneyLeader.CompanyName} ({WholeDollars(moneyLeader.TotalAmountRequestedUsd)}) despite fewer reports."));
        }

        insights.Add(new DashboardInsight($"{kpis.PendingReports} reports are waiting for moderation."));
        if (kpis.ChecksThisMonth == 0)
        {
            insights.Add(new DashboardInsight("No recruiter checks were logged this month."));
        }
        else
        {
            var monthPct = Math.Round(100.0 * highChecksThisMonth / kpis.ChecksThisMonth, 1, MidpointRounding.AwayFromZero);
            insights.Add(new DashboardInsight(
                $"{monthPct.ToString("0.#", CultureInfo.InvariantCulture)}% of recruiter checks were HIGH risk this month."));
        }

        return insights;
    }

    private async Task<string> ResolveChecksMonthAsync(CancellationToken cancellationToken)
    {
        var latestCheck = await repository.GetLatestCheckDateAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(latestCheck) && latestCheck.Length >= 7)
        {
            return latestCheck[..7];
        }

        return DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<CategoryCount> OrderStatuses(IReadOnlyList<CategoryCount> statuses)
    {
        string[] order = ["Pending", "Approved", "Rejected"];
        return order
            .Select(name => statuses.FirstOrDefault(row => row.Category == name) ?? new CategoryCount(name, 0))
            .ToList();
    }

    private static string WholeDollars(double amount) =>
        amount.ToString("$#,##0", CultureInfo.InvariantCulture);

    private static string MonthName(string yearMonth)
    {
        if (!DateOnly.TryParseExact(yearMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return yearMonth;
        }

        return date.ToString("MMMM", CultureInfo.InvariantCulture);
    }
}
