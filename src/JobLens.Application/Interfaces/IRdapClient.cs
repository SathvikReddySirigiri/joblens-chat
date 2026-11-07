using JobLens.Application.Dtos;

namespace JobLens.Application.Interfaces;

public interface IRdapClient
{
    Task<RdapResult> LookupAsync(string domain, CancellationToken cancellationToken);
}
