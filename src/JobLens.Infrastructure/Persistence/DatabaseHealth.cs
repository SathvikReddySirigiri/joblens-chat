using Microsoft.Data.Sqlite;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

/// <summary>Cheap readiness probe used by GET /healthz.</summary>
public static class DatabaseHealth
{
    public static async Task<bool> TryPingAsync(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = SqliteConnections.ForApp(configuration, environment);
            await using var connection = await SqliteConnections.OpenAsync(connectionString, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null && Convert.ToInt32(result) == 1;
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
