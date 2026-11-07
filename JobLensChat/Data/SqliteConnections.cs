using Microsoft.Data.Sqlite;

namespace JobLensChat.Data;

internal static class SqliteConnections
{
    public static string ForPath(string path) => $"Data Source={path};Foreign Keys=True";

    public static string ForApp(IConfiguration configuration, IWebHostEnvironment environment) =>
        ForPath(DatabasePath.Resolve(configuration, environment));

    public static async Task<SqliteConnection> OpenAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
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
