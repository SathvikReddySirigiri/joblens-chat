using JobLensChat.Data;
using Microsoft.Data.Sqlite;

namespace JobLensChat.Tests;

public sealed class TempDatabase : IAsyncDisposable
{
    public TempDatabase(string path)
    {
        Path = path;
        ConnectionString = SqliteConnections.ForPath(path);
    }

    public string Path { get; }

    public string ConnectionString { get; }

    public static async Task<TempDatabase> CreateAsync()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "joblens-" + Guid.NewGuid().ToString("N") + ".db");
        await DatabaseInitializer.CreateEmptyDatabaseAsync(path);
        return new TempDatabase(path);
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }

        await Task.CompletedTask;
    }
}
