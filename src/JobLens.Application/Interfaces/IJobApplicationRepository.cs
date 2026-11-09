using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface IJobApplicationRepository
{
    Task<PageResult<JobApplication>> BrowseAsync(
        int userId,
        string? status,
        string? search,
        int page,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<JobApplication>> ListForExportAsync(
        int userId,
        string? status,
        string? search,
        CancellationToken cancellationToken);

    Task<JobApplication?> GetByIdForUserAsync(int applicationId, int userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<JobApplicationStatusCount>> CountByStatusAsync(int userId, CancellationToken cancellationToken);

    Task<int> CountAllAsync(CancellationToken cancellationToken);

    Task<int> CreateAsync(
        int userId,
        string companyName,
        int? companyId,
        string roleTitle,
        string source,
        string? jobUrl,
        string? recruiterEmail,
        string status,
        string? appliedDate,
        string? nextFollowUpDate,
        string? notes,
        string? riskVerdict,
        string? closedDate,
        string createdAt,
        string updatedAt,
        CancellationToken cancellationToken);

    Task<bool> UpdateAsync(
        int applicationId,
        int userId,
        string companyName,
        int? companyId,
        string roleTitle,
        string source,
        string? jobUrl,
        string? recruiterEmail,
        string status,
        string? appliedDate,
        string? nextFollowUpDate,
        string? notes,
        string? riskVerdict,
        string? closedDate,
        string updatedAt,
        CancellationToken cancellationToken);

    Task<bool> UpdateStatusAsync(
        int applicationId,
        int userId,
        string status,
        string? closedDate,
        string updatedAt,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int applicationId, int userId, CancellationToken cancellationToken);
}
