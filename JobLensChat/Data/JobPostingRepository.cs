using JobLensChat.Models;
using Microsoft.Data.Sqlite;

namespace JobLensChat.Data;

public sealed class JobPostingRepository
{
    private const string SelectSql = """
        SELECT p.PostingId, p.CompanyId, c.Name, p.Title, p.Location, p.State,
               p.IsRemote, p.IsPaid, p.PayRatePerHour, p.Description, p.PostedDate
        FROM JobPostings p
        INNER JOIN Companies c ON c.CompanyId = p.CompanyId
        """;

    private readonly string _connectionString;

    public JobPostingRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal JobPostingRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<JobPosting>> ListAsync(
        string? search,
        int? companyId,
        bool? isRemote,
        bool? isPaid,
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
            WHERE ($search = '' OR p.Title LIKE $like OR p.Location LIKE $like)
              AND ($companyId = 0 OR p.CompanyId = $companyId)
              AND ($remote = -1 OR p.IsRemote = $remote)
              AND ($paid = -1 OR p.IsPaid = $paid)
            ORDER BY p.PostedDate DESC, p.PostingId DESC
            LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$search", term);
        command.Parameters.AddWithValue("$like", term.Length == 0 ? "" : "%" + term + "%");
        command.Parameters.AddWithValue("$companyId", companyId ?? 0);
        command.Parameters.AddWithValue("$remote", isRemote is null ? -1 : isRemote.Value ? 1 : 0);
        command.Parameters.AddWithValue("$paid", isPaid is null ? -1 : isPaid.Value ? 1 : 0);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<JobPosting?> GetByIdAsync(int postingId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectSql} WHERE p.PostingId = $id;";
        command.Parameters.AddWithValue("$id", postingId);
        var rows = await ReadListAsync(command, cancellationToken);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<int> CreateAsync(
        int companyId,
        string title,
        string location,
        string state,
        bool isRemote,
        bool isPaid,
        double? payRatePerHour,
        string description,
        string postedDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO JobPostings (
                CompanyId, Title, Location, State, IsRemote, IsPaid, PayRatePerHour, Description, PostedDate)
            VALUES ($companyId, $title, $location, $state, $remote, $paid, $pay, $description, $postedDate);
            SELECT last_insert_rowid();
            """;
        BindWrite(command, companyId, title, location, state, isRemote, isPaid, payRatePerHour, description, postedDate);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> UpdateAsync(
        int postingId,
        int companyId,
        string title,
        string location,
        string state,
        bool isRemote,
        bool isPaid,
        double? payRatePerHour,
        string description,
        string postedDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE JobPostings
            SET CompanyId = $companyId, Title = $title, Location = $location, State = $state,
                IsRemote = $remote, IsPaid = $paid, PayRatePerHour = $pay,
                Description = $description, PostedDate = $postedDate
            WHERE PostingId = $id;
            """;
        BindWrite(command, companyId, title, location, state, isRemote, isPaid, payRatePerHour, description, postedDate);
        command.Parameters.AddWithValue("$id", postingId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(int postingId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM JobPostings WHERE PostingId = $id;";
        command.Parameters.AddWithValue("$id", postingId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static void BindWrite(
        SqliteCommand command,
        int companyId,
        string title,
        string location,
        string state,
        bool isRemote,
        bool isPaid,
        double? payRatePerHour,
        string description,
        string postedDate)
    {
        command.Parameters.AddWithValue("$companyId", companyId);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$location", location);
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$remote", isRemote ? 1 : 0);
        command.Parameters.AddWithValue("$paid", isPaid ? 1 : 0);
        command.Parameters.AddWithValue("$pay", (object?)payRatePerHour ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$postedDate", postedDate);
    }

    private static async Task<IReadOnlyList<JobPosting>> ReadListAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<JobPosting>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new JobPosting(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6) == 1,
                reader.GetInt32(7) == 1,
                reader.IsDBNull(8) ? null : reader.GetDouble(8),
                reader.GetString(9),
                reader.GetString(10)));
        }

        return rows;
    }

    private static string Clean(string? value)
    {
        var text = value?.Trim() ?? "";
        return text.Replace("%", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
    }
}
