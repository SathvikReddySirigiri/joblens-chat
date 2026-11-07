using JobLensChat.Models;
using Microsoft.Data.Sqlite;

namespace JobLensChat.Data;

public sealed class CheckLogRepository
{
    private readonly string _connectionString;

    public CheckLogRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal CheckLogRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> AddAsync(CheckLog log, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CheckLogs (CheckType, InputSummary, RiskScore, Verdict, CreatedAt)
            VALUES ($type, $summary, $score, $verdict, $createdAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$type", log.CheckType);
        command.Parameters.AddWithValue("$summary", log.InputSummary);
        command.Parameters.AddWithValue("$score", log.RiskScore);
        command.Parameters.AddWithValue("$verdict", log.Verdict);
        command.Parameters.AddWithValue("$createdAt", log.CreatedAt);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<VerdictMonthCount>> CountsByVerdictPerMonthAsync(CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT substr(CreatedAt, 1, 7) AS Month, Verdict, COUNT(*)
            FROM CheckLogs
            GROUP BY Month, Verdict
            ORDER BY Month, Verdict
            LIMIT 100;
            """;
        var rows = new List<VerdictMonthCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new VerdictMonthCount(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CheckLog>> RecentChecksAsync(int limit, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(limit, 1, 20);
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CheckId, CheckType, InputSummary, RiskScore, Verdict, CreatedAt
            FROM CheckLogs
            ORDER BY CreatedAt DESC, CheckId DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", size);
        var rows = new List<CheckLog>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CheckLog(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return rows;
    }
}
