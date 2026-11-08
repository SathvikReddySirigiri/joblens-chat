using JobLens.Application.Dtos;

namespace JobLens.Application.Interfaces;

public interface ILiveJobsProvider
{
    string Name { get; }

    string AttributionText { get; }

    string AttributionUrl { get; }

    Task<LiveJobsResult> SearchAsync(LiveJobsQuery query, CancellationToken cancellationToken);
}
