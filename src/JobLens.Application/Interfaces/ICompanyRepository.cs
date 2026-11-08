using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface ICompanyRepository
{
    Task<CompanyActivitySummary> GetActivityAsync(int companyId, CancellationToken cancellationToken);
    Task<CompanyProfile?> GetProfileAsync(string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken);
    Task<Company?> GetByIdAsync(int companyId, CancellationToken cancellationToken);
    Task<int> CreateAsync( string name, string officialDomain, string industry, string hqState, CancellationToken cancellationToken);
    Task<bool> UpdateAsync( int companyId, string name, string officialDomain, string industry, string hqState, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int companyId, CancellationToken cancellationToken);
    Task<int> CountDependentsAsync(int companyId, CancellationToken cancellationToken);
    Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken);
    Task<bool> ExistsByDomainAsync(string domain, int? excludeId, CancellationToken cancellationToken);
}
