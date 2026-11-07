using JobLensChat.Models;
using Microsoft.Data.Sqlite;

namespace JobLensChat.Data;

public sealed partial class DashboardRepository
{
    private readonly string _connectionString;

    public DashboardRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal DashboardRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public Task<IReadOnlyList<MonthCount>> ReportsPerMonthAsync(CancellationToken cancellationToken) =>
        ReadMonthsAsync(
            """
            SELECT substr(ReportedDate, 1, 7) AS Month, COUNT(*)
            FROM ScamReports
            GROUP BY Month
            ORDER BY Month
            LIMIT 24;
            """,
            cancellationToken);

    public Task<IReadOnlyList<CategoryCount>> ReportsByScamTypeAsync(CancellationToken cancellationToken) =>
        ReadCategoriesAsync("ScamType", cancellationToken);

    public Task<IReadOnlyList<CategoryCount>> ReportsByStatusAsync(CancellationToken cancellationToken) =>
        ReadCategoriesAsync("Status", cancellationToken);

    public async Task<IReadOnlyList<CompanyRanking>> TopImpersonatedCompaniesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.Name, c.OfficialDomain, c.Industry, COUNT(*), COALESCE(SUM(r.AmountRequestedUsd), 0)
            FROM Companies c
            INNER JOIN ScamReports r ON r.CompanyId = c.CompanyId
            WHERE r.Status = 'Approved'
            GROUP BY c.CompanyId, c.Name, c.OfficialDomain, c.Industry
            ORDER BY COUNT(*) DESC, c.Name
            LIMIT 10;
            """;
        var rows = new List<CompanyRanking>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CompanyRanking(
                rows.Count + 1,
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetDouble(4)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<VerdictMonthCount>> ChecksByVerdictPerMonthAsync(CancellationToken cancellationToken)
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

    public async Task<IReadOnlyList<StateCount>> PostingsByStateAsync(CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT State, COUNT(*)
            FROM JobPostings
            GROUP BY State
            ORDER BY COUNT(*) DESC, State
            LIMIT 60;
            """;
        var rows = new List<StateCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StateCount(reader.GetString(0), reader.GetInt32(1)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CompanyRating>> AverageRatingByCompanyAsync(CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.Name, ROUND(AVG(r.Rating), 2), COUNT(*)
            FROM InterviewReviews r
            INNER JOIN Companies c ON c.CompanyId = r.CompanyId
            GROUP BY c.CompanyId, c.Name
            ORDER BY AVG(r.Rating) DESC, c.Name
            LIMIT 50;
            """;
        var rows = new List<CompanyRating>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CompanyRating(reader.GetString(0), reader.GetDouble(1), reader.GetInt32(2)));
        }

        return rows;
    }

    private async Task<IReadOnlyList<MonthCount>> ReadMonthsAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var rows = new List<MonthCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MonthCount(reader.GetString(0), reader.GetInt32(1)));
        }

        return rows;
    }

    private async Task<IReadOnlyList<CategoryCount>> ReadCategoriesAsync(string column, CancellationToken cancellationToken)
    {
        if (column is not ("ScamType" or "Status"))
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {column}, COUNT(*)
            FROM ScamReports
            GROUP BY {column}
            ORDER BY COUNT(*) DESC, {column}
            LIMIT 20;
            """;
        var rows = new List<CategoryCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CategoryCount(reader.GetString(0), reader.GetInt32(1)));
        }

        return rows;
    }
}
