using JobLensChat.Models;
using Microsoft.Data.Sqlite;

namespace JobLensChat.Data;

public sealed class ScamReportRepository
{
    private const string ReportSelect = """
        SELECT r.ReportId, r.CompanyId, c.Name, r.RecruiterEmail, r.Domain, r.ScamType, r.Channel,
               r.AmountRequestedUsd, r.DomainAgeDays, r.Status, r.ReportedDate, r.Description, r.CreatedByUserId
        FROM ScamReports r
        INNER JOIN Companies c ON c.CompanyId = r.CompanyId
        """;

    private readonly string _connectionString;

    public ScamReportRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal ScamReportRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<ScamReport>> ListAsync(
        string? search,
        string? status,
        string? scamType,
        int? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (limit, offset) = SqliteConnections.Page(page, pageSize);
        var term = Clean(search);
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {ReportSelect}
            WHERE ($search = '' OR r.RecruiterEmail LIKE $like OR r.Domain LIKE $like OR r.Description LIKE $like)
              AND ($status = '' OR r.Status = $status)
              AND ($scamType = '' OR r.ScamType = $scamType)
              AND ($companyId = 0 OR r.CompanyId = $companyId)
            ORDER BY r.ReportedDate DESC, r.ReportId DESC
            LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$search", term);
        command.Parameters.AddWithValue("$like", term.Length == 0 ? "" : "%" + term + "%");
        command.Parameters.AddWithValue("$status", Clean(status));
        command.Parameters.AddWithValue("$scamType", Clean(scamType));
        command.Parameters.AddWithValue("$companyId", companyId ?? 0);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<ScamReport?> GetByIdAsync(int reportId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {ReportSelect}
            WHERE r.ReportId = $id;
            """;
        command.Parameters.AddWithValue("$id", reportId);
        var rows = await ReadListAsync(command, cancellationToken);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<int> CreateAsync(
        int companyId,
        string recruiterEmail,
        string domain,
        string scamType,
        string channel,
        double amountRequestedUsd,
        int domainAgeDays,
        string status,
        string reportedDate,
        string description,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ScamReports (
                CompanyId, RecruiterEmail, Domain, ScamType, Channel,
                AmountRequestedUsd, DomainAgeDays, Status, ReportedDate, Description)
            VALUES (
                $companyId, $email, $domain, $scamType, $channel,
                $amount, $age, $status, $reportedDate, $description);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$companyId", companyId);
        command.Parameters.AddWithValue("$email", recruiterEmail);
        command.Parameters.AddWithValue("$domain", domain);
        command.Parameters.AddWithValue("$scamType", scamType);
        command.Parameters.AddWithValue("$channel", channel);
        command.Parameters.AddWithValue("$amount", amountRequestedUsd);
        command.Parameters.AddWithValue("$age", domainAgeDays);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$reportedDate", reportedDate);
        command.Parameters.AddWithValue("$description", description);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> UpdateAsync(
        int reportId,
        int companyId,
        string recruiterEmail,
        string domain,
        string scamType,
        string channel,
        double amountRequestedUsd,
        int domainAgeDays,
        string description,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ScamReports
            SET CompanyId = $companyId,
                RecruiterEmail = $email,
                Domain = $domain,
                ScamType = $scamType,
                Channel = $channel,
                AmountRequestedUsd = $amount,
                DomainAgeDays = $age,
                Description = $description
            WHERE ReportId = $id;
            """;
        command.Parameters.AddWithValue("$companyId", companyId);
        command.Parameters.AddWithValue("$email", recruiterEmail);
        command.Parameters.AddWithValue("$domain", domain);
        command.Parameters.AddWithValue("$scamType", scamType);
        command.Parameters.AddWithValue("$channel", channel);
        command.Parameters.AddWithValue("$amount", amountRequestedUsd);
        command.Parameters.AddWithValue("$age", domainAgeDays);
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$id", reportId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(int reportId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ScamReports WHERE ReportId = $id;";
        command.Parameters.AddWithValue("$id", reportId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> SetStatusAsync(int reportId, string status, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ScamReports SET Status = $status WHERE ReportId = $id;";
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$id", reportId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> ExistsDuplicateAsync(
        string email,
        string scamType,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM ScamReports
            WHERE RecruiterEmail = $email COLLATE NOCASE
              AND ScamType = $scamType
              AND ($excludeId = 0 OR ReportId <> $excludeId);
            """;
        command.Parameters.AddWithValue("$email", email);
        command.Parameters.AddWithValue("$scamType", scamType);
        command.Parameters.AddWithValue("$excludeId", excludeId ?? 0);
        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<IReadOnlyList<ScamReport>> ReadListAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var reports = new List<ScamReport>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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
                reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12)));
        }

        return reports;
    }

    private static string Clean(string? value)
    {
        var text = value?.Trim() ?? "";
        return text.Replace("%", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
    }
}
