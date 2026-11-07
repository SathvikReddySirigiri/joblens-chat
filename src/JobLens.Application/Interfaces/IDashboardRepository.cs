using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface IDashboardRepository
{
    Task<IReadOnlyList<MonthCount>> ReportsPerMonthAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryCount>> ReportsByScamTypeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryCount>> ReportsByStatusAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyRanking>> TopImpersonatedCompaniesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<VerdictMonthCount>> ChecksByVerdictPerMonthAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StateCount>> PostingsByStateAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyRating>> AverageRatingByCompanyAsync(CancellationToken cancellationToken);
    Task<string?> LatestReportedDateAsync(CancellationToken cancellationToken);
    Task<(DashboardKpis Kpis, int HighChecksThisMonth)> ReadKpisAsync(string? fromDate, string? toDate, string currentMonth, CancellationToken cancellationToken);
    Task<IReadOnlyList<MonthSeries>> ReportsPerMonthAsync(string? fromDate, string? toDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryCount>> CountsByAsync( string column, string? fromDate, string? toDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyRanking>> TopApprovedCompaniesAsync( string? fromDate, string? toDate, CancellationToken cancellationToken);
    Task<CompanyRanking?> HighestApprovedAmountAsync( string? fromDate, string? toDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<CheckMonth>> ChecksByMonthAsync(string? fromDate, string? toDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyCompare>> PostingsVsReportsAsync( string? fromDate, string? toDate, CancellationToken cancellationToken);
}
