using JobLens.Application.Dtos;

namespace JobLens.Application.Interfaces;

public interface IEdgarClient
{
    Task<EdgarResult> MatchCompany(string name, CancellationToken cancellationToken);
}