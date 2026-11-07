using JobLens.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobLens.Infrastructure.Persistence;

public sealed class DatabaseAdmin(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<DatabaseAdmin> logger) : IDatabaseAdmin
{
    public string ResolvePath() => DatabasePath.Resolve(configuration, environment);

    public Task ResetAsync(CancellationToken cancellationToken) =>
        DatabaseInitializer.ResetAsync(configuration, environment, logger, cancellationToken);
}
