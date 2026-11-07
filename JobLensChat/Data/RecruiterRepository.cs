using JobLensChat.Models;
using Microsoft.Data.Sqlite;

namespace JobLensChat.Data;

public sealed class RecruiterRepository
{
    private const string SelectSql = """
        SELECT r.RecruiterId, r.Email, r.Domain, r.CompanyId, c.Name, r.FirstSeen, r.Notes, COUNT(s.ReportId)
        FROM Recruiters r
        LEFT JOIN Companies c ON c.CompanyId = r.CompanyId
        LEFT JOIN ScamReports s ON s.RecruiterEmail = r.Email COLLATE NOCASE
        """;

    private readonly string _connectionString;

    public RecruiterRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal RecruiterRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<Recruiter>> ListAsync(
        string? search,
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
            {SelectSql}
            WHERE ($search = '' OR r.Email LIKE $like OR r.Domain LIKE $like)
              AND ($companyId = 0 OR r.CompanyId = $companyId)
            GROUP BY r.RecruiterId, r.Email, r.Domain, r.CompanyId, c.Name, r.FirstSeen, r.Notes
            ORDER BY r.Email
            LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$search", term);
        command.Parameters.AddWithValue("$like", term.Length == 0 ? "" : "%" + term + "%");
        command.Parameters.AddWithValue("$companyId", companyId ?? 0);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<Recruiter?> GetByIdAsync(int recruiterId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectSql}
            WHERE r.RecruiterId = $id
            GROUP BY r.RecruiterId, r.Email, r.Domain, r.CompanyId, c.Name, r.FirstSeen, r.Notes;
            """;
        command.Parameters.AddWithValue("$id", recruiterId);
        var rows = await ReadListAsync(command, cancellationToken);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<Recruiter?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectSql}
            WHERE r.Email = $email COLLATE NOCASE
            GROUP BY r.RecruiterId, r.Email, r.Domain, r.CompanyId, c.Name, r.FirstSeen, r.Notes;
            """;
        command.Parameters.AddWithValue("$email", email.Trim());
        var rows = await ReadListAsync(command, cancellationToken);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<int> CreateAsync(
        string email,
        string domain,
        int? companyId,
        string firstSeen,
        string notes,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Recruiters (Email, Domain, CompanyId, FirstSeen, Notes)
            VALUES ($email, $domain, $companyId, $firstSeen, $notes);
            SELECT last_insert_rowid();
            """;
        Bind(command, email, domain, companyId, firstSeen, notes);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> UpdateAsync(
        int recruiterId,
        string email,
        string domain,
        int? companyId,
        string firstSeen,
        string notes,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Recruiters
            SET Email = $email, Domain = $domain, CompanyId = $companyId, FirstSeen = $firstSeen, Notes = $notes
            WHERE RecruiterId = $id;
            """;
        Bind(command, email, domain, companyId, firstSeen, notes);
        command.Parameters.AddWithValue("$id", recruiterId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(int recruiterId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Recruiters WHERE RecruiterId = $id;";
        command.Parameters.AddWithValue("$id", recruiterId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM Recruiters
            WHERE Email = $email COLLATE NOCASE
              AND ($excludeId = 0 OR RecruiterId <> $excludeId);
            """;
        command.Parameters.AddWithValue("$email", email);
        command.Parameters.AddWithValue("$excludeId", excludeId ?? 0);
        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static void Bind(
        SqliteCommand command,
        string email,
        string domain,
        int? companyId,
        string firstSeen,
        string notes)
    {
        command.Parameters.AddWithValue("$email", email);
        command.Parameters.AddWithValue("$domain", domain);
        command.Parameters.AddWithValue("$companyId", (object?)companyId ?? DBNull.Value);
        command.Parameters.AddWithValue("$firstSeen", firstSeen);
        command.Parameters.AddWithValue("$notes", notes);
    }

    private static async Task<IReadOnlyList<Recruiter>> ReadListAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var rows = new List<Recruiter>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new Recruiter(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7)));
        }

        return rows;
    }

    private static string Clean(string? value)
    {
        var text = value?.Trim() ?? "";
        return text.Replace("%", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
    }
}
