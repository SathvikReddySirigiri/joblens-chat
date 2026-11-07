using System.Globalization;
using JobLensChat.Data;
using JobLensChat.Models;

namespace JobLensChat.Services;

public sealed class DashboardService(DashboardRepository repository)
{
    public async Task<DashboardSnapshot> GetAsync(string? range, CancellationToken cancellationToken)
    {
        var latest = await repository.LatestReportedDateAsync(cancellationToken);
        var (from, to, label) = Window(range, latest);
        var month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var (kpis, highThisMonth) = await repository.ReadKpisAsync(from, to, month, cancellationToken);
        var months = await repository.ReportsPerMonthAsync(from, to, cancellationToken);
        var types = await repository.CountsByAsync("ScamType", from, to, cancellationToken);
        var statuses = await repository.CountsByAsync("Status", from, to, cancellationToken);
        var top = await repository.TopApprovedCompaniesAsync(from, to, cancellationToken);
        var moneyLeader = await repository.HighestApprovedAmountAsync(from, to, cancellationToken);
        var checks = await repository.ChecksByMonthAsync(from, to, cancellationToken);
        var channels = await repository.CountsByAsync("Channel", from, to, cancellationToken);
        var compare = await repository.PostingsVsReportsAsync(from, to, cancellationToken);
        var statusesInOrder = OrderStatuses(statuses);
        var insights = BuildInsights(kpis, months, top, moneyLeader, highThisMonth);
        return new DashboardSnapshot(kpis, months, types, statusesInOrder, top, checks, channels, compare, insights, latest, label);
    }

    internal static (string? From, string? To, string Label) Window(string? range, string? latestReportedDate)
    {
        var label = string.IsNullOrWhiteSpace(range) ? "all" : range.Trim().ToLowerInvariant();
        if (label is not ("3m" or "6m") || string.IsNullOrWhiteSpace(latestReportedDate)
            || !DateOnly.TryParseExact(latestReportedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var latest))
        {
            return (null, null, "all");
        }

        var monthsBack = label == "3m" ? 3 : 6;
        var from = latest.AddMonths(-monthsBack).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (from, latest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), label);
    }

    internal static IReadOnlyList<string> BuildInsights(
        DashboardKpis kpis,
        IReadOnlyList<MonthSeries> months,
        IReadOnlyList<CompanyRanking> top,
        CompanyRanking? moneyLeader,
        int highChecksThisMonth)
    {
        var insights = new List<string>();
        var july = months.FirstOrDefault(month => month.Month.EndsWith("-07", StringComparison.Ordinal));
        var october = months.FirstOrDefault(month => month.Month.EndsWith("-10", StringComparison.Ordinal));
        if (july is not null && october is not null && july.Total > 0)
        {
            var change = (october.Total - july.Total) * 100.0 / july.Total;
            var verb = change >= 0 ? "grew" : "fell";
            insights.Add($"Reports {verb} {Math.Abs(change).ToString("0.#", CultureInfo.InvariantCulture)}% from July to October.");
        }
        else if (months.Count >= 2 && months[0].Total > 0)
        {
            var change = (months[^1].Total - months[0].Total) * 100.0 / months[0].Total;
            var verb = change >= 0 ? "grew" : "fell";
            insights.Add($"Reports {verb} {Math.Abs(change).ToString("0.#", CultureInfo.InvariantCulture)}% from {MonthName(months[0].Month)} to {MonthName(months[^1].Month)}.");
        }

        if (top.Count > 0)
        {
            var leader = top[0];
            insights.Add(
                $"{leader.CompanyName} is the most impersonated company ({leader.ApprovedReports} approved reports, {leader.TotalAmountRequestedUsd.ToString("$#,##0.00", CultureInfo.InvariantCulture)} requested).");
        }

        if (top.Count > 0 && moneyLeader is not null && moneyLeader.ApprovedReports < top[0].ApprovedReports)
        {
            insights.Add($"Highest money requested: {moneyLeader.CompanyName} ({WholeDollars(moneyLeader.TotalAmountRequestedUsd)}) despite fewer reports.");
        }

        insights.Add($"{kpis.PendingReports} reports are waiting for moderation.");
        if (kpis.ChecksThisMonth == 0)
        {
            insights.Add("No recruiter checks were logged this month.");
        }
        else
        {
            var monthPct = Math.Round(100.0 * highChecksThisMonth / kpis.ChecksThisMonth, 1, MidpointRounding.AwayFromZero);
            insights.Add($"{monthPct.ToString("0.#", CultureInfo.InvariantCulture)}% of recruiter checks were HIGH risk this month.");
        }

        return insights;
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
