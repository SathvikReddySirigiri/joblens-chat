using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface IResumeRepository
{
    Task<Resume?> GetByUserIdAsync(int userId, CancellationToken cancellationToken);

    Task UpsertAsync(
        int userId,
        string fileName,
        string redactedText,
        string skillsCsv,
        int privacyIssueCount,
        string privacyFindingsJson,
        string uploadedAt,
        CancellationToken cancellationToken);

    Task<bool> DeleteByUserIdAsync(int userId, CancellationToken cancellationToken);

    Task<bool> ExistsByUserIdAsync(int userId, CancellationToken cancellationToken);
}
