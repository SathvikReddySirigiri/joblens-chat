using JobLens.Domain.Entities;
using JobLens.Application.Dtos;
using Microsoft.Data.Sqlite;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

public sealed class CheckLogRepository : ICheckLogRepository
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
            INSERT INTO CheckLogs (CheckType, InputSummary, RiskScore, Verdict, CreatedAt, UserId)
            VALUES ($type, $summary, $score, $verdict, $createdAt, $userId);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$type", log.CheckType);
        command.Parameters.AddWithValue("$summary", log.InputSummary);
        command.Parameters.AddWithValue("$score", log.RiskScore);
        command.Parameters.AddWithValue("$verdict", log.Verdict);
        command.Parameters.AddWithValue("$createdAt", log.CreatedAt);
        command.Parameters.AddWithValue("$userId", log.UserId is int id ? id : DBNull.Value);
        var idValue = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(idValue, System.Globalization.CultureInfo.InvariantCulture);
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

    public Task<IReadOnlyList<CheckLog>> RecentChecksAsync(int limit, CancellationToken cancellationToken) =>
        RecentCoreAsync(userId: null, limit, cancellationToken);

    public Task<IReadOnlyList<CheckLog>> RecentForUserAsync(int userId, int limit, CancellationToken cancellationToken) =>
        RecentCoreAsync(userId, limit, cancellationToken);

    private async Task<IReadOnlyList<CheckLog>> RecentCoreAsync(int? userId, int limit, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(limit, 1, 20);
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CheckId, CheckType, InputSummary, RiskScore, Verdict, CreatedAt, UserId
            FROM CheckLogs
            WHERE ($filterUser = 0 OR UserId = $userId)
            ORDER BY CreatedAt DESC, CheckId DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$filterUser", userId.HasValue ? 1 : 0);
        command.Parameters.AddWithValue("$userId", userId ?? 0);
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
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6)));
        }

        return rows;
    }
}
