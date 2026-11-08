using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface IJobPostingRepository
{
    Task<PageResult<JobPosting>> BrowseAsync( string? search, int? companyId, bool? isRemote, bool? isPaid, string? state, int page, CancellationToken cancellationToken);
    Task<IReadOnlyList<JobPosting>> ListAsync( string? search, int? companyId, bool? isRemote, bool? isPaid, int page, int pageSize, CancellationToken cancellationToken);
    Task<JobPosting?> GetByIdAsync(int postingId, CancellationToken cancellationToken);
    Task<int> CreateAsync( int companyId, string title, string location, string state, bool isRemote, bool isPaid, double? payRatePerHour, string description, string postedDate, CancellationToken cancellationToken);
    Task<bool> UpdateAsync( int postingId, int companyId, string title, string location, string state, bool isRemote, bool isPaid, double? payRatePerHour, string description, string postedDate, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int postingId, CancellationToken cancellationToken);
    Task<PageResult<JobPostingListItem>> SearchPageAsync(SearchFilter filter, int page, CancellationToken cancellationToken);
    Task<IReadOnlyList<JobListing>> SearchListedAsync(SearchFilter filter, int limit, CancellationToken cancellationToken);
}
