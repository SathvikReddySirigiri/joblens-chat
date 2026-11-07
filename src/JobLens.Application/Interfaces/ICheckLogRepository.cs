using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface ICheckLogRepository
{
    Task<int> AddAsync(CheckLog log, CancellationToken cancellationToken);
    Task<IReadOnlyList<VerdictMonthCount>> CountsByVerdictPerMonthAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CheckLog>> RecentChecksAsync(int limit, CancellationToken cancellationToken);
}
