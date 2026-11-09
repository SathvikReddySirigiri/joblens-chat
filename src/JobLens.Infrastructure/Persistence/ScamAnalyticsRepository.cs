using JobLens.Domain.Entities;
using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Dashboard;
using Microsoft.Data.Sqlite;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

public sealed class ScamAnalyticsRepository : IScamAnalyticsRepository
{
    private const string ReportSelect = """
        SELECT r.ReportId, r.CompanyId, c.Name, r.RecruiterEmail, r.Domain, r.ScamType, r.Channel,
               r.AmountRequestedUsd, r.DomainAgeDays, r.Status, r.ReportedDate, r.Description
        FROM ScamReports r
        INNER JOIN Companies c ON c.CompanyId = r.CompanyId
        """;

    private readonly string _connectionString;

    public ScamAnalyticsRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal ScamAnalyticsRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<ScamReport>> GetReportsByDomainAsync(string domain, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {ReportSelect}
            WHERE r.Domain = $domain COLLATE NOCASE
            ORDER BY r.ReportedDate DESC, r.ReportId DESC
            LIMIT 20;
            """;
        command.Parameters.AddWithValue("$domain", Clean(domain));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadReportsAsync(reader, cancellationToken);
    }

    public async Task<IReadOnlyList<ScamReport>> SearchReportsAsync(
        string? scamType,
        string? companyName,
        string? channel,
        string? status,
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Empty strings mean "no filter" so every value stays a bound parameter.
        command.CommandText = $"""
            {ReportSelect}
            WHERE ($scamType = '' OR r.ScamType = $scamType COLLATE NOCASE)
              AND ($companyName = '' OR c.Name = $companyName COLLATE NOCASE)
              AND ($channel = '' OR r.Channel = $channel COLLATE NOCASE)
              AND ($status = '' OR r.Status = $status COLLATE NOCASE)
              AND ($fromDate = '' OR r.ReportedDate >= $fromDate)
              AND ($toDate = '' OR r.ReportedDate <= $toDate)
            ORDER BY r.ReportedDate DESC, r.ReportId DESC
            LIMIT 20;
            """;
        command.Parameters.AddWithValue("$scamType", Clean(scamType));
        command.Parameters.AddWithValue("$companyName", Clean(companyName));
        command.Parameters.AddWithValue("$channel", Clean(channel));
        command.Parameters.AddWithValue("$status", Clean(status));
        command.Parameters.AddWithValue("$fromDate", Clean(fromDate));
        command.Parameters.AddWithValue("$toDate", Clean(toDate));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadReportsAsync(reader, cancellationToken);
    }

    public async Task<ScamSummary> SummarizeAsync(string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        var from = Clean(fromDate);
        var to = Clean(toDate);

        await using var connection = await OpenAsync(cancellationToken);
        await using var totals = connection.CreateCommand();
        totals.CommandText = """
            SELECT
                COUNT(*),
                COALESCE(SUM(CASE WHEN Status = 'Approved' THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN Status = 'Rejected' THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(AmountRequestedUsd), 0),
                COALESCE(AVG(AmountRequestedUsd), 0)
            FROM ScamReports
            WHERE ($fromDate = '' OR ReportedDate >= $fromDate)
              AND ($toDate = '' OR ReportedDate <= $toDate);
            """;
        totals.Parameters.AddWithValue("$fromDate", from);
        totals.Parameters.AddWithValue("$toDate", to);

        int total;
        int approved;
        int pending;
        int rejected;
        double totalAmount;
        double average;
        await using (var reader = await totals.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            total = reader.GetInt32(0);
            approved = reader.GetInt32(1);
            pending = reader.GetInt32(2);
            rejected = reader.GetInt32(3);
            totalAmount = reader.GetDouble(4);
            average = Math.Round(reader.GetDouble(5), 2, MidpointRounding.AwayFromZero);
        }

        var byType = await ReadCategoriesAsync(connection, "ScamType", from, to, cancellationToken);
        var byChannel = await ReadCategoriesAsync(connection, "Channel", from, to, cancellationToken);

        return new ScamSummary(
            total,
            approved,
            pending,
            rejected,
            totalAmount,
            average,
            from.Length == 0 ? null : from,
            to.Length == 0 ? null : to,
            byType,
            byChannel);
    }

    public async Task<IReadOnlyList<CompanyRanking>> TopImpersonatedCompaniesAsync(
        int count,
        string? fromDate,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(count, 1, 10);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.Name, c.OfficialDomain, c.Industry,
                   COUNT(*) AS ApprovedReports,
                   COALESCE(SUM(r.AmountRequestedUsd), 0) AS TotalAmount
            FROM Companies c
            INNER JOIN ScamReports r ON r.CompanyId = c.CompanyId
            WHERE r.Status = 'Approved'
              AND ($fromDate = '' OR r.ReportedDate >= $fromDate)
            GROUP BY c.CompanyId, c.Name, c.OfficialDomain, c.Industry
            ORDER BY ApprovedReports DESC, c.Name
            LIMIT $count;
            """;
        command.Parameters.AddWithValue("$fromDate", Clean(fromDate));
        command.Parameters.AddWithValue("$count", limit);

        var rankings = new List<CompanyRanking>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rankings.Add(new CompanyRanking(
                rankings.Count + 1,
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetDouble(4)));
        }

        return rankings;
    }

    public async Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CompanyId, Name, OfficialDomain, Industry, HqState
            FROM Companies
            ORDER BY Name
            LIMIT 50;
            """;

        var companies = new List<Company>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            companies.Add(new Company(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)));
        }

        return companies;
    }

    public async Task<int> CountApprovedReportsForDomainAsync(string domain, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM ScamReports
            WHERE Domain = $domain COLLATE NOCASE
              AND Status = 'Approved';
            """;
        command.Parameters.AddWithValue("$domain", Clean(domain));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<DataCounts> GetCountsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM Companies),
                (SELECT COUNT(*) FROM ScamReports),
                (SELECT COUNT(*) FROM ScamReports WHERE Status = 'Approved'),
                (SELECT COUNT(*) FROM ScamReports WHERE Status = 'Pending'),
                (SELECT COUNT(*) FROM ScamReports WHERE Status = 'Rejected'),
                (SELECT COUNT(*) FROM JobPostings),
                (SELECT COUNT(*) FROM InterviewReviews),
                (SELECT COUNT(*) FROM CheckLogs),
                (SELECT COUNT(*) FROM Recruiters),
                (SELECT COUNT(*) FROM Users),
                (SELECT COUNT(*) FROM JobApplications);
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new DataCounts(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt32(9),
            reader.GetInt32(10));
    }

    public async Task<(int ApprovedReports, int CompaniesImpersonated, double AmountRequestedUsd, int RecruiterChecks)> GetHomeImpactStatsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM ScamReports WHERE Status = 'Approved'),
                (SELECT COUNT(DISTINCT CompanyId) FROM ScamReports WHERE Status = 'Approved'),
                (SELECT COALESCE(SUM(AmountRequestedUsd), 0) FROM ScamReports WHERE Status = 'Approved'),
                (SELECT COUNT(*) FROM CheckLogs WHERE CheckType = 'Recruiter');
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetDouble(2),
            reader.GetInt32(3));
    }

    public async Task<IReadOnlyList<ScamReport>> GetLatestApprovedReportsAsync(int limit, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(limit, 1, 20);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {ReportSelect}
            WHERE r.Status = 'Approved'
            ORDER BY r.ReportedDate DESC, r.ReportId DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", size);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadReportsAsync(reader, cancellationToken);
    }

    public async Task<int> CountPendingReportsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ScamReports WHERE Status = 'Pending';";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<int> CountReportsSinceAsync(string fromDateInclusive, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM ScamReports
            WHERE ReportedDate >= $fromDate;
            """;
        command.Parameters.AddWithValue("$fromDate", Clean(fromDateInclusive));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<IReadOnlyList<CategoryCount>> ReadCategoriesAsync(
        SqliteConnection connection,
        string column,
        string from,
        string to,
        CancellationToken cancellationToken)
    {
        // column is one of two fixed identifiers, never a request value.
        if (column is not ("ScamType" or "Channel"))
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {column}, COUNT(*)
            FROM ScamReports
            WHERE ($fromDate = '' OR ReportedDate >= $fromDate)
              AND ($toDate = '' OR ReportedDate <= $toDate)
            GROUP BY {column}
            ORDER BY COUNT(*) DESC, {column};
            """;
        command.Parameters.AddWithValue("$fromDate", from);
        command.Parameters.AddWithValue("$toDate", to);

        var counts = new List<CategoryCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts.Add(new CategoryCount(reader.GetString(0), reader.GetInt32(1)));
        }

        return counts;
    }

    private static async Task<IReadOnlyList<ScamReport>> ReadReportsAsync(
        SqliteDataReader reader,
        CancellationToken cancellationToken)
    {
        var reports = new List<ScamReport>();
        while (await reader.ReadAsync(cancellationToken))
        {
            reports.Add(new ScamReport(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetDouble(7),
                reader.GetInt32(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetString(11)));
        }

        return reports;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
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

    private static string Clean(string? value) => value?.Trim() ?? "";
}


