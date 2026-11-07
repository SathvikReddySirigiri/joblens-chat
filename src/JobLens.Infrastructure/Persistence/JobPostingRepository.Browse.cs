using JobLens.Domain.Entities;
using JobLens.Application.Dtos;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

public sealed partial class JobPostingRepository : IJobPostingRepository
{
    public async Task<PageResult<JobPosting>> BrowseAsync(
        string? search,
        int? companyId,
        bool? isRemote,
        bool? isPaid,
        string? state,
        int page,
        CancellationToken cancellationToken)
    {
        var (limit, offset) = SqliteConnections.Page(page, JobLens.Domain.Constants.Paging.PageSize);
        var term = Clean(search);
        var stateCode = (state ?? "").Trim().ToUpperInvariant();
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = $"""
            SELECT COUNT(*)
            FROM JobPostings p
            INNER JOIN Companies c ON c.CompanyId = p.CompanyId
            WHERE {PostingFilter};
            """;
        BindPosting(count, term, companyId, isRemote, isPaid, stateCode);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectSql}
            WHERE {PostingFilter}
            ORDER BY p.PostedDate DESC, p.PostingId DESC
            LIMIT $limit OFFSET $offset;
            """;
        BindPosting(command, term, companyId, isRemote, isPaid, stateCode);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        var items = await ReadListAsync(command, cancellationToken);
        return new PageResult<JobPosting>(items, Math.Max(page, 1), limit, total);
    }

    private const string PostingFilter = """
        ($search = '' OR p.Title LIKE $like OR p.Location LIKE $like OR c.Name LIKE $like)
          AND ($companyId = 0 OR p.CompanyId = $companyId)
          AND ($remote = -1 OR p.IsRemote = $remote)
          AND ($paid = -1 OR p.IsPaid = $paid)
          AND ($state = '' OR p.State = $state)
        """;

    private static void BindPosting(
        Microsoft.Data.Sqlite.SqliteCommand command,
        string term,
        int? companyId,
        bool? isRemote,
        bool? isPaid,
        string state)
    {
        command.Parameters.AddWithValue("$search", term);
        command.Parameters.AddWithValue("$like", term.Length == 0 ? "" : "%" + term + "%");
        command.Parameters.AddWithValue("$companyId", companyId ?? 0);
        command.Parameters.AddWithValue("$remote", isRemote is null ? -1 : isRemote.Value ? 1 : 0);
        command.Parameters.AddWithValue("$paid", isPaid is null ? -1 : isPaid.Value ? 1 : 0);
        command.Parameters.AddWithValue("$state", state);
    }
}
