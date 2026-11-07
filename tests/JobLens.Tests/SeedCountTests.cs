using JobLens.Infrastructure.Persistence;
using JobLens.Application.Services;
using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Infrastructure.External;
using JobLens.Infrastructure.AI;
using JobLens.Infrastructure.Documents;
using JobLens.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class SeedCountTests
{
    [Fact]
    public async Task Full_seed_fills_every_table_without_storing_passwords()
    {
        var root = Path.Combine(Path.GetTempPath(), "joblens-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var webRoot = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(webRoot);
        var environment = new TestHost { ContentRootPath = root, WebRootPath = webRoot };
        var configuration = new ConfigurationBuilder().Build();

        try
        {
            await DatabaseInitializer.InitializeAsync(configuration, environment, NullLogger.Instance);
            var counts = await new ScamRepository(configuration, environment).GetCountsAsync(CancellationToken.None);
            Assert.Equal(20, counts.Companies);
            Assert.Equal(130, counts.Recruiters);
            Assert.Equal(120, counts.ScamReports);
            Assert.Equal(85, counts.Approved);
            Assert.Equal(23, counts.Pending);
            Assert.Equal(12, counts.Rejected);
            Assert.Equal(70, counts.JobPostings);
            Assert.Equal(50, counts.InterviewReviews);
            Assert.Equal(100, counts.CheckLogs);
            Assert.Equal(5, counts.Users);

            var database = SqliteConnections.ForPath(Path.Combine(root, "App_Data", "joblens.db"));
            await using var connection = await SqliteConnections.OpenAsync(database, CancellationToken.None);
            Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE CreatedByUserId IS NOT NULL;"));
            Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Description = '';"));
            Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM InterviewReviews WHERE CreatedByUserId IS NOT NULL;"));
            Assert.Equal(10, await ScalarAsync(connection, "SELECT COUNT(*) FROM Recruiters WHERE Notes = 'Verified official domain';"));
            Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM Users WHERE PasswordHash LIKE '%2026%';"));

            var hash = await TextAsync(connection, "SELECT PasswordHash FROM Users WHERE Email = 'student1@joblens.demo';");
            Assert.True(UserPassword.Verify("student1@joblens.demo", hash, "Student#2026"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static async Task<int> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string> TextAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    private sealed class TestHost : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "JobLens.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = "";

        public string EnvironmentName { get; set; } = "Development";

        public string WebRootPath { get; set; } = "";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
