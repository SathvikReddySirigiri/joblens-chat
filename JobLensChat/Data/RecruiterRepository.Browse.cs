using JobLensChat.Models;

namespace JobLensChat.Data;

public sealed partial class RecruiterRepository
{
    public async Task<PageResult<Recruiter>> BrowseAsync(
        string? search,
        int? companyId,
        int page,
        CancellationToken cancellationToken)
    {
        var (limit, offset) = SqliteConnections.Page(page, JobLensChat.Catalog.PageSize);
        var term = Clean(search);
        const string where = """
            ($search = '' OR r.Email LIKE $like OR r.Domain LIKE $like OR c.Name LIKE $like)
              AND ($companyId = 0 OR r.CompanyId = $companyId)
            """;
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = $"""
            SELECT COUNT(*)
            FROM Recruiters r
            LEFT JOIN Companies c ON c.CompanyId = r.CompanyId
            WHERE {where};
            """;
        BindBrowse(count, term, companyId);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectSql}
            WHERE {where}
            GROUP BY r.RecruiterId, r.Email, r.Domain, r.CompanyId, c.Name, c.OfficialDomain, r.FirstSeen, r.Notes
            ORDER BY r.Email
            LIMIT $limit OFFSET $offset;
            """;
        BindBrowse(command, term, companyId);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        var items = await ReadListAsync(command, cancellationToken);
        return new PageResult<Recruiter>(items, Math.Max(page, 1), limit, total);
    }

    private static void BindBrowse(Microsoft.Data.Sqlite.SqliteCommand command, string term, int? companyId)
    {
        command.Parameters.AddWithValue("$search", term);
        command.Parameters.AddWithValue("$like", term.Length == 0 ? "" : "%" + term + "%");
        command.Parameters.AddWithValue("$companyId", companyId ?? 0);
    }
}
