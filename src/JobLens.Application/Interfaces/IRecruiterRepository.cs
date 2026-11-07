using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface IRecruiterRepository
{
    Task<PageResult<Recruiter>> BrowseAsync( string? search, int? companyId, int page, CancellationToken cancellationToken);
    Task<IReadOnlyList<Recruiter>> ListAsync( string? search, int? companyId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Recruiter?> GetByIdAsync(int recruiterId, CancellationToken cancellationToken);
    Task<Recruiter?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<int> CreateAsync( string email, string domain, int? companyId, string firstSeen, string notes, CancellationToken cancellationToken);
    Task<bool> UpdateAsync( int recruiterId, string email, string domain, int? companyId, string firstSeen, string notes, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int recruiterId, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, int? excludeId, CancellationToken cancellationToken);
}
