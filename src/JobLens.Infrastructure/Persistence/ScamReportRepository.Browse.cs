using JobLens.Domain.Entities;
using JobLens.Application.Dtos;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

public sealed partial class ScamReportRepository : IScamReportRepository
{
    public async Task<PageResult<ScamReport>> BrowseAsync(
        string? search,
        string? status,
        string? scamType,
        string? channel,
        int? companyId,
        string? sort,
        bool descending,
        int page,
        int viewerUserId,
        bool isModerator,
        CancellationToken cancellationToken)
    {
        var (limit, offset) = SqliteConnections.Page(page, JobLens.Domain.Constants.Paging.PageSize);
        var term = Clean(search);
        var order = sort switch
        {
            "amount" => descending ? "r.AmountRequestedUsd DESC" : "r.AmountRequestedUsd ASC",
            "status" => descending ? "r.Status DESC" : "r.Status ASC",
            "company" => descending ? "c.Name DESC" : "c.Name ASC",
            _ => descending ? "r.ReportedDate DESC" : "r.ReportedDate ASC",
        };
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        var total = await ScalarAsync(connection, $"""
            SELECT COUNT(*)
            FROM ScamReports r
            INNER JOIN Companies c ON c.CompanyId = r.CompanyId
            WHERE {ReportFilter}
            """, term, status, scamType, channel, companyId, viewerUserId, isModerator, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {ReportSelect}
            WHERE {ReportFilter}
            ORDER BY {order}, r.ReportId DESC
            LIMIT $limit OFFSET $offset;
            """;
        BindFilter(command, term, status, scamType, channel, companyId, viewerUserId, isModerator);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        var items = await ReadListAsync(command, cancellationToken);
        return new PageResult<ScamReport>(items, Math.Max(page, 1), limit, total);
    }

    public async Task<IReadOnlyList<ScamReport>> OthersOnDomainAsync(
        string domain,
        int excludeId,
        int limit,
        int viewerUserId,
        bool isModerator,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {ReportSelect}
            WHERE r.Domain = $domain COLLATE NOCASE
              AND r.ReportId <> $excludeId
              AND ({VisibilityFilter})
            ORDER BY r.ReportedDate DESC, r.ReportId DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$domain", domain);
        command.Parameters.AddWithValue("$excludeId", excludeId);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5));
        command.Parameters.AddWithValue("$viewerUserId", viewerUserId);
        command.Parameters.AddWithValue("$isModerator", isModerator ? 1 : 0);
        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<ScamReport>> GetApprovedByExactDomainAsync(
        string domain,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {ReportSelect}
            WHERE r.Domain = $domain COLLATE NOCASE
              AND r.Status = 'Approved'
            ORDER BY r.ReportedDate DESC, r.ReportId DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$domain", Clean(domain));
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5));
        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<int?> FindDuplicateIdAsync(
        string email,
        string scamType,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ReportId
            FROM ScamReports
            WHERE RecruiterEmail = $email COLLATE NOCASE
              AND ScamType = $scamType
              AND ($excludeId = 0 OR ReportId <> $excludeId)
            ORDER BY ReportId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$email", email);
        command.Parameters.AddWithValue("$scamType", scamType);
        command.Parameters.AddWithValue("$excludeId", excludeId ?? 0);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<int> CountByStatusAsync(string status, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ScamReports WHERE Status = $status;";
        command.Parameters.AddWithValue("$status", status);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private const string VisibilityFilter = """
        $isModerator = 1
          OR r.Status = 'Approved'
          OR ($viewerUserId <> 0 AND r.CreatedByUserId = $viewerUserId)
        """;

    private const string ReportFilter = $"""
        ($search = '' OR r.RecruiterEmail LIKE $like OR r.Domain LIKE $like OR r.Description LIKE $like OR c.Name LIKE $like)
          AND ($status = '' OR r.Status = $status)
          AND ($scamType = '' OR r.ScamType = $scamType)
          AND ($channel = '' OR r.Channel = $channel)
          AND ($companyId = 0 OR r.CompanyId = $companyId)
          AND ({VisibilityFilter})
        """;

    private static async Task<int> ScalarAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string sql,
        string term,
        string? status,
        string? scamType,
        string? channel,
        int? companyId,
        int viewerUserId,
        bool isModerator,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        BindFilter(command, term, status, scamType, channel, companyId, viewerUserId, isModerator);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void BindFilter(
        Microsoft.Data.Sqlite.SqliteCommand command,
        string term,
        string? status,
        string? scamType,
        string? channel,
        int? companyId,
        int viewerUserId,
        bool isModerator)
    {
        command.Parameters.AddWithValue("$search", term);
        command.Parameters.AddWithValue("$like", term.Length == 0 ? "" : "%" + term + "%");
        command.Parameters.AddWithValue("$status", Clean(status));
        command.Parameters.AddWithValue("$scamType", Clean(scamType));
        command.Parameters.AddWithValue("$channel", Clean(channel));
        command.Parameters.AddWithValue("$companyId", companyId ?? 0);
        command.Parameters.AddWithValue("$viewerUserId", viewerUserId);
        command.Parameters.AddWithValue("$isModerator", isModerator ? 1 : 0);
    }
}
