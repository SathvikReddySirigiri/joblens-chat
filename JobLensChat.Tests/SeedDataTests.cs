using System.Globalization;
using JobLensChat.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace JobLensChat.Tests;

public sealed class SeedDataTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Expanded_seed_keeps_known_answers_and_prints_aggregates()
    {
        var root = Path.Combine(Path.GetTempPath(), "joblens-seed-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var webRoot = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(webRoot);
        var environment = new TestHost { ContentRootPath = root, WebRootPath = webRoot };
        var configuration = new ConfigurationBuilder().Build();

        try
        {
            await DatabaseInitializer.InitializeAsync(configuration, environment, NullLogger.Instance);
            var database = SqliteConnections.ForPath(Path.Combine(root, "App_Data", "joblens.db"));
            await using var connection = await SqliteConnections.OpenAsync(database, CancellationToken.None);

            Assert.Equal(20, await ScalarAsync(connection, "SELECT COUNT(*) FROM Companies;"));
            Assert.Equal(120, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports;"));
            Assert.Equal(130, await ScalarAsync(connection, "SELECT COUNT(*) FROM Recruiters;"));
            Assert.Equal(70, await ScalarAsync(connection, "SELECT COUNT(*) FROM JobPostings;"));
            Assert.Equal(50, await ScalarAsync(connection, "SELECT COUNT(*) FROM InterviewReviews;"));
            Assert.Equal(100, await ScalarAsync(connection, "SELECT COUNT(*) FROM CheckLogs;"));
            Assert.Equal(5, await ScalarAsync(connection, "SELECT COUNT(*) FROM Users;"));
            Assert.Equal(85, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Status = 'Approved';"));
            Assert.Equal(23, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Status = 'Pending';"));
            Assert.Equal(12, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Status = 'Rejected';"));

            var top = await RowsAsync(connection, """
                SELECT c.Name, COUNT(*)
                FROM Companies c
                INNER JOIN ScamReports r ON r.CompanyId = c.CompanyId
                WHERE r.Status = 'Approved'
                GROUP BY c.CompanyId, c.Name
                ORDER BY COUNT(*) DESC, c.Name
                LIMIT 5;
                """);
            Assert.Equal(["Amazon", "Microsoft", "Deloitte", "Google", "JPMorgan Chase"], top.Select(row => row.Label).ToArray());
            Assert.Equal([15, 13, 10, 9, 7], top.Select(row => row.Count).ToArray());

            Assert.Equal(4, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Domain = 'amaz0n-careers.net';"));
            Assert.Equal(3, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Domain = 'amaz0n-careers.net' AND Status = 'Approved';"));
            Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Domain = 'amaz0n-careers.net' AND Status = 'Pending';"));
            Assert.Equal(3, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Domain = 'rnicrosoft-hiring.com';"));
            Assert.Equal(3, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Domain = 'rnicrosoft-hiring.com' AND Status = 'Approved';"));
            Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Domain = 'safe-company-example.com';"));
            Assert.Equal(120, await ScalarAsync(connection, "SELECT COUNT(DISTINCT LOWER(RecruiterEmail)) FROM ScamReports;"));

            foreach (var type in new[] { "Fake check", "Equipment purchase", "Upfront fee", "Chat-only interview", "Identity theft", "Fake staffing agency" })
            {
                var count = await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE ScamType = $value;", type);
                Assert.True(count >= 10, $"{type} appears {count} times.");
            }

            foreach (var channel in new[] { "Email", "Telegram", "WhatsApp", "LinkedIn", "Text message" })
            {
                var count = await ScalarAsync(connection, "SELECT COUNT(*) FROM ScamReports WHERE Channel = $value;", channel);
                Assert.True(count >= 10, $"{channel} appears {count} times.");
            }

            var equipment = await ScalarAsync(connection, """
                SELECT COUNT(*) FROM ScamReports
                WHERE ScamType = 'Equipment purchase' AND Status = 'Approved' AND ReportedDate >= '2026-09-01';
                """);
            var deloitteTelegram = await ScalarAsync(connection, """
                SELECT COUNT(*) FROM ScamReports r
                INNER JOIN Companies c ON c.CompanyId = r.CompanyId
                WHERE c.Name = 'Deloitte' AND r.Channel = 'Telegram';
                """);
            var total = await DoubleAsync(connection, "SELECT COALESCE(SUM(AmountRequestedUsd), 0) FROM ScamReports;");
            var average = await DoubleAsync(connection, "SELECT COALESCE(AVG(AmountRequestedUsd), 0) FROM ScamReports;");
            var types = await RowsAsync(connection, "SELECT ScamType, COUNT(*) FROM ScamReports GROUP BY ScamType ORDER BY ScamType;");
            var channels = await RowsAsync(connection, "SELECT Channel, COUNT(*) FROM ScamReports GROUP BY Channel ORDER BY Channel;");
            var topAmounts = await RowsAsync(connection, """
                SELECT c.Name, COUNT(*), COALESCE(SUM(r.AmountRequestedUsd), 0)
                FROM Companies c
                INNER JOIN ScamReports r ON r.CompanyId = c.CompanyId
                WHERE r.Status = 'Approved'
                GROUP BY c.CompanyId, c.Name
                ORDER BY COUNT(*) DESC, COALESCE(SUM(r.AmountRequestedUsd), 0) DESC, c.Name
                LIMIT 5;
                """);

            Write($"Approved Equipment purchase since 2026-09-01: {equipment}");
            Write($"Deloitte + Telegram reports: {deloitteTelegram}");
            Write($"Total AmountRequestedUsd: {total.ToString("0.00", CultureInfo.InvariantCulture)}");
            Write($"Average AmountRequestedUsd: {average.ToString("0.00", CultureInfo.InvariantCulture)}");
            Write("By scam type: " + string.Join(" · ", types.Select(row => $"{row.Label} {row.Count}")));
            Write("By channel: " + string.Join(" · ", channels.Select(row => $"{row.Label} {row.Count}")));
            Write("Top approved: " + string.Join(" · ", topAmounts.Select(row => $"{row.Label} {row.Count} ({row.Amount.ToString("0.00", CultureInfo.InvariantCulture)})")));
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

    private void Write(string line)
    {
        output.WriteLine(line);
        Console.WriteLine(line);
    }

    private static async Task<int> ScalarAsync(SqliteConnection connection, string sql, string? value = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (value is not null)
        {
            command.Parameters.AddWithValue("$value", value);
        }

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<double> DoubleAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return Convert.ToDouble(result, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<(string Label, int Count, double Amount)>> RowsAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var rows = new List<(string, int, double)>();
        await using var reader = await command.ExecuteReaderAsync();
        var hasAmount = reader.FieldCount > 2;
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture), hasAmount ? Convert.ToDouble(reader.GetValue(2), CultureInfo.InvariantCulture) : 0));
        }

        return rows;
    }

    private sealed class TestHost : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "JobLensChat.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = "";

        public string EnvironmentName { get; set; } = "Development";

        public string WebRootPath { get; set; } = "";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
