using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;

namespace JobLens.Application.Services;

public sealed class HomeService(IScamQueryRepository queries)
{
    public Task<DataCounts> GetCountsAsync(CancellationToken cancellationToken) =>
        queries.GetCountsAsync(cancellationToken);
}
