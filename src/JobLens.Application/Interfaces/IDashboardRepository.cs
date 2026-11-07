using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Dashboard;
using DashboardCompanyActivity = JobLens.Application.Dtos.Dashboard.CompanyActivity;

namespace JobLens.Application.Interfaces;

public interface IDashboardRepository
{
    Task<string?> GetLatestReportDateAsync(CancellationToken cancellationToken);

    Task<string?> GetLatestCheckDateAsync(CancellationToken cancellationToken);

    Task<(DashboardKpis Kpis, int HighChecksThisMonth)> GetKpisAsync(
        DateRange range,
        string checksMonth,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthCount>> GetReportsPerMonthAsync(DateRange range, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryCount>> GetReportsByScamTypeAsync(DateRange range, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryCount>> GetReportsByStatusAsync(DateRange range, CancellationToken cancellationToken);

    Task<IReadOnlyList<CompanyImpersonation>> GetTopImpersonatedAsync(
        DateRange range,
        int top,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VerdictMonth>> GetChecksByVerdictPerMonthAsync(DateRange range, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryCount>> GetReportsByChannelAsync(DateRange range, CancellationToken cancellationToken);

    Task<IReadOnlyList<DashboardCompanyActivity>> GetCompanyActivityAsync(
        DateRange range,
        int top,
        CancellationToken cancellationToken);
}
