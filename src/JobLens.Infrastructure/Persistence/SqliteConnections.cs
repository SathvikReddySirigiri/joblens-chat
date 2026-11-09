using Microsoft.Data.Sqlite;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

internal static class SqliteConnections
{
    // Rollback journal (not WAL): Azure's HOME/data content share does not support WAL shared-memory
    // files reliably. Busy timeout 5s + open/close per operation lets concurrent requests wait briefly
    // instead of failing, without holding a long-lived connection across App Service recycles.
    public static string ForPath(string path) =>
        $"Data Source={path};Foreign Keys=True;Default Timeout=5";

    public static string ForApp(IConfiguration configuration, IWebHostEnvironment environment) =>
        ForPath(DatabasePath.Resolve(configuration, environment));

    public static async Task<SqliteConnection> OpenAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=DELETE; PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public static (int Limit, int Offset) Page(int page, int pageSize, int maxSize = 50)
    {
        var size = Math.Clamp(pageSize, 1, maxSize);
        var index = Math.Max(page, 1);
        return (size, (index - 1) * size);
    }
}
