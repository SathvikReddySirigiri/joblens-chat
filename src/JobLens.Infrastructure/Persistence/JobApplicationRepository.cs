using System.Globalization;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Domain.Constants;
using JobLens.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace JobLens.Infrastructure.Persistence;

public sealed class JobApplicationRepository : IJobApplicationRepository
{
    private const string SelectSql = """
        SELECT ApplicationId, UserId, CompanyName, CompanyId, RoleTitle, Source, JobUrl, RecruiterEmail,
               Status, AppliedDate, NextFollowUpDate, Notes, RiskVerdict, ClosedDate, CreatedAt, UpdatedAt
        FROM JobApplications
        """;

    private readonly string _connectionString;

    public JobApplicationRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal JobApplicationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<PageResult<JobApplication>> BrowseAsync(
        int userId,
        string? status,
        string? search,
        int page,
        CancellationToken cancellationToken)
    {
        var (limit, offset) = SqliteConnections.Page(page, Paging.PageSize);
        var term = Clean(search);
        var statusFilter = string.IsNullOrWhiteSpace(status) ? "" : status.Trim();
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = """
            SELECT COUNT(*)
            FROM JobApplications
            WHERE UserId = $userId
              AND ($status = '' OR Status = $status)
              AND ($search = '' OR CompanyName LIKE $like OR RoleTitle LIKE $like OR IFNULL(Notes, '') LIKE $like);
            """;
        BindBrowseFilters(countCommand, userId, statusFilter, term);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectSql}
            WHERE UserId = $userId
              AND ($status = '' OR Status = $status)
              AND ($search = '' OR CompanyName LIKE $like OR RoleTitle LIKE $like OR IFNULL(Notes, '') LIKE $like)
            ORDER BY
              CASE WHEN NextFollowUpDate IS NULL OR NextFollowUpDate = '' THEN 1 ELSE 0 END,
              NextFollowUpDate ASC,
              UpdatedAt DESC,
              ApplicationId DESC
            LIMIT $limit OFFSET $offset;
            """;
        BindBrowseFilters(command, userId, statusFilter, term);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        var items = await ReadListAsync(command, cancellationToken);
        return new PageResult<JobApplication>(items, Math.Max(page, 1), limit, total);
    }

    public async Task<IReadOnlyList<JobApplication>> ListForExportAsync(
        int userId,
        string? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var term = Clean(search);
        var statusFilter = string.IsNullOrWhiteSpace(status) ? "" : status.Trim();
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectSql}
            WHERE UserId = $userId
              AND ($status = '' OR Status = $status)
              AND ($search = '' OR CompanyName LIKE $like OR RoleTitle LIKE $like OR IFNULL(Notes, '') LIKE $like)
            ORDER BY UpdatedAt DESC, ApplicationId DESC
            LIMIT 2000;
            """;
        BindBrowseFilters(command, userId, statusFilter, term);
        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<JobApplication?> GetByIdForUserAsync(int applicationId, int userId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectSql} WHERE ApplicationId = $id AND UserId = $userId;";
        command.Parameters.AddWithValue("$id", applicationId);
        command.Parameters.AddWithValue("$userId", userId);
        var rows = await ReadListAsync(command, cancellationToken);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<IReadOnlyList<JobApplicationStatusCount>> CountByStatusAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Status, COUNT(*)
            FROM JobApplications
            WHERE UserId = $userId
            GROUP BY Status;
            """;
        command.Parameters.AddWithValue("$userId", userId);
        var rows = new List<JobApplicationStatusCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new JobApplicationStatusCount(reader.GetString(0), reader.GetInt32(1)));
        }

        return rows;
    }

    public async Task<int> CountAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM JobApplications;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public async Task<int> CreateAsync(
        int userId,
        string companyName,
        int? companyId,
        string roleTitle,
        string source,
        string? jobUrl,
        string? recruiterEmail,
        string status,
        string? appliedDate,
        string? nextFollowUpDate,
        string? notes,
        string? riskVerdict,
        string? closedDate,
        string createdAt,
        string updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO JobApplications (
                UserId, CompanyName, CompanyId, RoleTitle, Source, JobUrl, RecruiterEmail,
                Status, AppliedDate, NextFollowUpDate, Notes, RiskVerdict, ClosedDate, CreatedAt, UpdatedAt)
            VALUES (
                $userId, $companyName, $companyId, $roleTitle, $source, $jobUrl, $recruiterEmail,
                $status, $appliedDate, $nextFollowUpDate, $notes, $riskVerdict, $closedDate, $createdAt, $updatedAt);
            SELECT last_insert_rowid();
            """;
        BindWrite(
            command,
            userId,
            companyName,
            companyId,
            roleTitle,
            source,
            jobUrl,
            recruiterEmail,
            status,
            appliedDate,
            nextFollowUpDate,
            notes,
            riskVerdict,
            closedDate,
            createdAt,
            updatedAt);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id, CultureInfo.InvariantCulture);
    }

    public async Task<bool> UpdateAsync(
        int applicationId,
        int userId,
        string companyName,
        int? companyId,
        string roleTitle,
        string source,
        string? jobUrl,
        string? recruiterEmail,
        string status,
        string? appliedDate,
        string? nextFollowUpDate,
        string? notes,
        string? riskVerdict,
        string? closedDate,
        string updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE JobApplications
            SET CompanyName = $companyName, CompanyId = $companyId, RoleTitle = $roleTitle, Source = $source,
                JobUrl = $jobUrl, RecruiterEmail = $recruiterEmail, Status = $status, AppliedDate = $appliedDate,
                NextFollowUpDate = $nextFollowUpDate, Notes = $notes, RiskVerdict = $riskVerdict,
                ClosedDate = $closedDate, UpdatedAt = $updatedAt
            WHERE ApplicationId = $id AND UserId = $userId;
            """;
        BindWrite(
            command,
            userId,
            companyName,
            companyId,
            roleTitle,
            source,
            jobUrl,
            recruiterEmail,
            status,
            appliedDate,
            nextFollowUpDate,
            notes,
            riskVerdict,
            closedDate,
            createdAt: null,
            updatedAt);
        command.Parameters.AddWithValue("$id", applicationId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> UpdateStatusAsync(
        int applicationId,
        int userId,
        string status,
        string? closedDate,
        string updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE JobApplications
            SET Status = $status, ClosedDate = $closedDate, UpdatedAt = $updatedAt
            WHERE ApplicationId = $id AND UserId = $userId;
            """;
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$closedDate", (object?)closedDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", updatedAt);
        command.Parameters.AddWithValue("$id", applicationId);
        command.Parameters.AddWithValue("$userId", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(int applicationId, int userId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM JobApplications WHERE ApplicationId = $id AND UserId = $userId;";
        command.Parameters.AddWithValue("$id", applicationId);
        command.Parameters.AddWithValue("$userId", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static void BindBrowseFilters(SqliteCommand command, int userId, string status, string term)
    {
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$search", term);
        command.Parameters.AddWithValue("$like", term.Length == 0 ? "" : "%" + term + "%");
    }

    private static void BindWrite(
        SqliteCommand command,
        int userId,
        string companyName,
        int? companyId,
        string roleTitle,
        string source,
        string? jobUrl,
        string? recruiterEmail,
        string status,
        string? appliedDate,
        string? nextFollowUpDate,
        string? notes,
        string? riskVerdict,
        string? closedDate,
        string? createdAt,
        string updatedAt)
    {
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$companyName", companyName);
        command.Parameters.AddWithValue("$companyId", (object?)companyId ?? DBNull.Value);
        command.Parameters.AddWithValue("$roleTitle", roleTitle);
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$jobUrl", (object?)jobUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$recruiterEmail", (object?)recruiterEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$appliedDate", (object?)appliedDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$nextFollowUpDate", (object?)nextFollowUpDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$riskVerdict", (object?)riskVerdict ?? DBNull.Value);
        command.Parameters.AddWithValue("$closedDate", (object?)closedDate ?? DBNull.Value);
        if (createdAt is not null)
        {
            command.Parameters.AddWithValue("$createdAt", createdAt);
        }

        command.Parameters.AddWithValue("$updatedAt", updatedAt);
    }

    private static async Task<IReadOnlyList<JobApplication>> ReadListAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var rows = new List<JobApplication>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new JobApplication(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.GetString(14),
                reader.GetString(15)));
        }

        return rows;
    }

    private static string Clean(string? value) => value?.Trim() ?? "";
}
