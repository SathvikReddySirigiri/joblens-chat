using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface IScamReportRepository
{
    Task<PageResult<ScamReport>> BrowseAsync( string? search, string? status, string? scamType, string? channel, int? companyId, string? sort, bool descending, int page, int viewerUserId, bool isModerator, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScamReport>> OthersOnDomainAsync( string domain, int excludeId, int limit, int viewerUserId, bool isModerator, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScamReport>> GetApprovedByExactDomainAsync(string domain, int limit, CancellationToken cancellationToken);
    Task<int?> FindDuplicateIdAsync( string email, string scamType, int? excludeId, CancellationToken cancellationToken);
    Task<int> CountByStatusAsync(string status, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScamReport>> ListAsync( string? search, string? status, string? scamType, int? companyId, int page, int pageSize, CancellationToken cancellationToken);
    Task<ScamReport?> GetByIdAsync(int reportId, CancellationToken cancellationToken);
    Task<int> CreateAsync( int companyId, string recruiterEmail, string domain, string scamType, string channel, double amountRequestedUsd, int domainAgeDays, string status, string reportedDate, string description, int? createdByUserId, CancellationToken cancellationToken);
    Task<bool> UpdateAsync( int reportId, int companyId, string recruiterEmail, string domain, string scamType, string channel, double amountRequestedUsd, int domainAgeDays, string description, string reportedDate, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int reportId, CancellationToken cancellationToken);
    Task<bool> SetStatusAsync(int reportId, string status, CancellationToken cancellationToken);
    Task<bool> ExistsDuplicateAsync( string email, string scamType, int? excludeId, CancellationToken cancellationToken);
}
