using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

/// <summary>Read-only analytics used by dashboard, home, risk scoring and MCP tools.</summary>
public interface IScamAnalyticsRepository
{
    Task<IReadOnlyList<ScamReport>> GetReportsByDomainAsync(string domain, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScamReport>> SearchReportsAsync( string? scamType, string? companyName, string? channel, string? status, string? fromDate, string? toDate, CancellationToken cancellationToken);
    Task<ScamSummary> SummarizeAsync(string? fromDate, string? toDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyRanking>> TopImpersonatedCompaniesAsync( int count, string? fromDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken cancellationToken);
    Task<int> CountApprovedReportsForDomainAsync(string domain, CancellationToken cancellationToken);
    Task<DataCounts> GetCountsAsync(CancellationToken cancellationToken);
}
