using JobLensChat.Models;
using Microsoft.Data.Sqlite;

namespace JobLensChat.Data;

public sealed partial class InterviewReviewRepository
{
    private const string SelectSql = """
        SELECT r.ReviewId, r.CompanyId, c.Name, r.Role, r.Rating, r.Experience, r.WasLegit, r.ReviewDate, r.CreatedByUserId
        FROM InterviewReviews r
        INNER JOIN Companies c ON c.CompanyId = r.CompanyId
        """;

    private readonly string _connectionString;

    public InterviewReviewRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal InterviewReviewRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<InterviewReview>> ListAsync(
        int? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (limit, offset) = SqliteConnections.Page(page, pageSize);
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectSql}
            WHERE ($companyId = 0 OR r.CompanyId = $companyId)
            ORDER BY r.ReviewDate DESC, r.ReviewId DESC
            LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$companyId", companyId ?? 0);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<InterviewReview?> GetByIdAsync(int reviewId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectSql} WHERE r.ReviewId = $id;";
        command.Parameters.AddWithValue("$id", reviewId);
        var rows = await ReadListAsync(command, cancellationToken);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<int> CreateAsync(
        int companyId,
        string role,
        int rating,
        string experience,
        bool wasLegit,
        string reviewDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO InterviewReviews (CompanyId, Role, Rating, Experience, WasLegit, ReviewDate)
            VALUES ($companyId, $role, $rating, $experience, $legit, $reviewDate);
            SELECT last_insert_rowid();
            """;
        BindWrite(command, companyId, role, rating, experience, wasLegit, reviewDate);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> UpdateAsync(
        int reviewId,
        int companyId,
        string role,
        int rating,
        string experience,
        bool wasLegit,
        string reviewDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE InterviewReviews
            SET CompanyId = $companyId, Role = $role, Rating = $rating,
                Experience = $experience, WasLegit = $legit, ReviewDate = $reviewDate
            WHERE ReviewId = $id;
            """;
        BindWrite(command, companyId, role, rating, experience, wasLegit, reviewDate);
        command.Parameters.AddWithValue("$id", reviewId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(int reviewId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM InterviewReviews WHERE ReviewId = $id;";
        command.Parameters.AddWithValue("$id", reviewId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
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
            ORDER BY c.Name
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

    private static void BindWrite(
        SqliteCommand command,
        int companyId,
        string role,
        int rating,
        string experience,
        bool wasLegit,
        string reviewDate)
    {
        command.Parameters.AddWithValue("$companyId", companyId);
        command.Parameters.AddWithValue("$role", role);
        command.Parameters.AddWithValue("$rating", rating);
        command.Parameters.AddWithValue("$experience", experience);
        command.Parameters.AddWithValue("$legit", wasLegit ? 1 : 0);
        command.Parameters.AddWithValue("$reviewDate", reviewDate);
    }

    private static async Task<IReadOnlyList<InterviewReview>> ReadListAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<InterviewReview>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new InterviewReview(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetInt32(6) == 1,
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8)));
        }

        return rows;
    }
}
