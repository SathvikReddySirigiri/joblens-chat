using System.Globalization;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace JobLens.Infrastructure.Persistence;

public sealed class ResumeRepository : IResumeRepository
{
    private readonly string _connectionString;

    public ResumeRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal ResumeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Resume?> GetByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ResumeId, UserId, FileName, RedactedText, SkillsCsv, PrivacyIssueCount, PrivacyFindingsJson, UploadedAt
            FROM Resumes
            WHERE UserId = $userId;
            """;
        command.Parameters.AddWithValue("$userId", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Resume(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.GetString(7));
    }

    public async Task UpsertAsync(
        int userId,
        string fileName,
        string redactedText,
        string skillsCsv,
        int privacyIssueCount,
        string privacyFindingsJson,
        string uploadedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Resumes (
                UserId, FileName, RedactedText, SkillsCsv, PrivacyIssueCount, PrivacyFindingsJson, UploadedAt)
            VALUES (
                $userId, $fileName, $redactedText, $skillsCsv, $privacyIssueCount, $findings, $uploadedAt)
            ON CONFLICT(UserId) DO UPDATE SET
                FileName = excluded.FileName,
                RedactedText = excluded.RedactedText,
                SkillsCsv = excluded.SkillsCsv,
                PrivacyIssueCount = excluded.PrivacyIssueCount,
                PrivacyFindingsJson = excluded.PrivacyFindingsJson,
                UploadedAt = excluded.UploadedAt;
            """;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$fileName", fileName);
        command.Parameters.AddWithValue("$redactedText", redactedText);
        command.Parameters.AddWithValue("$skillsCsv", skillsCsv);
        command.Parameters.AddWithValue("$privacyIssueCount", privacyIssueCount);
        command.Parameters.AddWithValue("$findings", privacyFindingsJson);
        command.Parameters.AddWithValue("$uploadedAt", uploadedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> DeleteByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Resumes WHERE UserId = $userId;";
        command.Parameters.AddWithValue("$userId", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> ExistsByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Resumes WHERE UserId = $userId;";
        command.Parameters.AddWithValue("$userId", userId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture) > 0;
    }
}
