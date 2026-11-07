using JobLensChat.Models;

namespace JobLensChat.Data;

public sealed partial class InterviewReviewRepository
{
    public async Task<PageResult<InterviewReview>> BrowseAsync(
        int? companyId,
        int? rating,
        bool? wasLegit,
        int page,
        CancellationToken cancellationToken)
    {
        var (limit, offset) = SqliteConnections.Page(page, JobLensChat.Catalog.PageSize);
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = $"""
            SELECT COUNT(*)
            FROM InterviewReviews r
            INNER JOIN Companies c ON c.CompanyId = r.CompanyId
            WHERE {ReviewFilter};
            """;
        BindReview(count, companyId, rating, wasLegit);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {SelectSql}
            WHERE {ReviewFilter}
            ORDER BY r.ReviewDate DESC, r.ReviewId DESC
            LIMIT $limit OFFSET $offset;
            """;
        BindReview(command, companyId, rating, wasLegit);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        var items = await ReadListAsync(command, cancellationToken);
        return new PageResult<InterviewReview>(items, Math.Max(page, 1), limit, total);
    }

    private const string ReviewFilter = """
        ($companyId = 0 OR r.CompanyId = $companyId)
          AND ($rating = 0 OR r.Rating = $rating)
          AND ($legit = -1 OR r.WasLegit = $legit)
        """;

    private static void BindReview(
        Microsoft.Data.Sqlite.SqliteCommand command,
        int? companyId,
        int? rating,
        bool? wasLegit)
    {
        command.Parameters.AddWithValue("$companyId", companyId ?? 0);
        command.Parameters.AddWithValue("$rating", rating is >= 1 and <= 5 ? rating.Value : 0);
        command.Parameters.AddWithValue("$legit", wasLegit is null ? -1 : wasLegit.Value ? 1 : 0);
    }
}
