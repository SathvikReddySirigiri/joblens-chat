using JobLens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobLens.Tests;

public sealed class TempDatabase : IAsyncDisposable
{
    private readonly string? _contentRoot;

    public TempDatabase(string path, string? contentRoot = null)
    {
        Path = path;
        ConnectionString = SqliteConnections.ForPath(path);
        _contentRoot = contentRoot;
    }

    public string Path { get; }

    public string ConnectionString { get; }

    public static async Task<TempDatabase> CreateAsync()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "joblens-" + Guid.NewGuid().ToString("N") + ".db");
        await DatabaseInitializer.CreateEmptyDatabaseAsync(path);
        return new TempDatabase(path);
    }

    public static async Task<TempDatabase> CreateSeededAsync()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "joblens-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var webRoot = System.IO.Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(webRoot);
        var environment = new SeedHost { ContentRootPath = root, WebRootPath = webRoot };
        var configuration = new ConfigurationBuilder().Build();
        await DatabaseInitializer.InitializeAsync(configuration, environment, NullLogger.Instance);
        var path = System.IO.Path.Combine(root, "App_Data", "joblens.db");
        return new TempDatabase(path, root);
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (_contentRoot is not null && Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, true);
        }
        else if (File.Exists(Path))
        {
            File.Delete(Path);
        }

        await Task.CompletedTask;
    }

    private sealed class SeedHost : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "JobLens.Tests";
        public string WebRootPath { get; set; } = "";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
