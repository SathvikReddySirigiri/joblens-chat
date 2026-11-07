using JobLensChat.Models;

namespace JobLensChat.Data;

public sealed partial class JobPostingRepository
{
    public async Task<PageResult<JobPosting>> SearchPageAsync(SearchFilter filter, int page, CancellationToken cancellationToken)
    {
        var (limit, offset) = SqliteConnections.Page(page, JobLensChat.Catalog.PageSize);
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = $"""
            SELECT COUNT(*)
            FROM JobPostings p
            INNER JOIN Companies c ON c.CompanyId = p.CompanyId
            WHERE {SmartFilter};
            """;
        BindSmart(count, filter);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectSql}
            WHERE {SmartFilter}
            ORDER BY p.PostedDate DESC, p.PostingId DESC
            LIMIT $limit OFFSET $offset;
            """;
        BindSmart(command, filter);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        var items = await ReadListAsync(command, cancellationToken);
        return new PageResult<JobPosting>(items, Math.Max(page, 1), limit, total);
    }

    public async Task<IReadOnlyList<JobListing>> SearchListedAsync(SearchFilter filter, int limit, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(limit, 1, 20);
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT p.PostingId, c.Name, p.Title, p.Location, p.State, p.IsRemote, p.IsPaid, p.PayRatePerHour,
                   CASE WHEN EXISTS (
                       SELECT 1 FROM ScamReports s
                       WHERE s.CompanyId = p.CompanyId AND s.Status = 'Approved') THEN 1 ELSE 0 END
            FROM JobPostings p
            INNER JOIN Companies c ON c.CompanyId = p.CompanyId
            WHERE {SmartFilter}
            ORDER BY p.PostedDate DESC, p.PostingId DESC
            LIMIT $limit;
            """;
        BindSmart(command, filter);
        command.Parameters.AddWithValue("$limit", take);
        var rows = new List<JobListing>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new JobListing(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5) == 1,
                reader.GetInt32(6) == 1,
                reader.IsDBNull(7) ? null : reader.GetDouble(7),
                reader.GetInt32(8) == 1));
        }

        return rows;
    }

    private const string SmartFilter = """
        ($search = '' OR p.Title LIKE $like OR p.Location LIKE $like OR c.Name LIKE $like)
          AND ($companyId = 0 OR p.CompanyId = $companyId)
          AND ($remote = -1 OR p.IsRemote = $remote)
          AND ($paid = -1 OR p.IsPaid = $paid)
          AND ($state = '' OR p.State = $state)
          AND ($minPay < 0 OR (p.IsPaid = 1 AND p.PayRatePerHour >= $minPay))
          AND ($exclude = 0 OR NOT EXISTS (
                SELECT 1 FROM ScamReports s
                WHERE s.CompanyId = p.CompanyId AND s.Status = 'Approved'))
        """;

    private static void BindSmart(Microsoft.Data.Sqlite.SqliteCommand command, SearchFilter filter)
    {
        var term = Clean(filter.Keyword);
        command.Parameters.AddWithValue("$search", term);
        command.Parameters.AddWithValue("$like", term.Length == 0 ? "" : "%" + term + "%");
        command.Parameters.AddWithValue("$companyId", filter.CompanyId ?? 0);
        command.Parameters.AddWithValue("$remote", filter.IsRemote is null ? -1 : filter.IsRemote.Value ? 1 : 0);
        command.Parameters.AddWithValue("$paid", filter.IsPaid is null ? -1 : filter.IsPaid.Value ? 1 : 0);
        command.Parameters.AddWithValue("$state", (filter.State ?? "").Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("$minPay", filter.MinPayPerHour ?? -1);
        command.Parameters.AddWithValue("$exclude", filter.ExcludeCompaniesWithApprovedReports ? 1 : 0);
    }
}
