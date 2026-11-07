using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface IInterviewReviewRepository
{
    Task<PageResult<InterviewReview>> BrowseAsync( int? companyId, int? rating, bool? wasLegit, int page, CancellationToken cancellationToken);
    Task<IReadOnlyList<InterviewReview>> ListAsync( int? companyId, int page, int pageSize, CancellationToken cancellationToken);
    Task<InterviewReview?> GetByIdAsync(int reviewId, CancellationToken cancellationToken);
    Task<int> CreateAsync( int companyId, string role, int rating, string experience, bool wasLegit, string reviewDate, CancellationToken cancellationToken);
    Task<bool> UpdateAsync( int reviewId, int companyId, string role, int rating, string experience, bool wasLegit, string reviewDate, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int reviewId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyRating>> AverageRatingByCompanyAsync(CancellationToken cancellationToken);
}
