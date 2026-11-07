using JobLens.Domain.Entities;
using JobLens.Application.Dtos;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

public sealed partial class DashboardRepository : IDashboardRepository
{
    public async Task<string?> LatestReportedDateAsync(CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(ReportedDate) FROM ScamReports;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    public async Task<(DashboardKpis Kpis, int HighChecksThisMonth)> ReadKpisAsync(
        string? fromDate,
        string? toDate,
        string currentMonth,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var reports = connection.CreateCommand();
        reports.CommandText = $"""
            SELECT COUNT(*),
                   COALESCE(SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN Status = 'Approved' THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN Status = 'Rejected' THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(AmountRequestedUsd), 0)
            FROM ScamReports
            WHERE {ReportWindow};
            """;
        BindWindow(reports, fromDate, toDate);
        await using var reportReader = await reports.ExecuteReaderAsync(cancellationToken);
        await reportReader.ReadAsync(cancellationToken);
        var total = reportReader.GetInt32(0);
        var pending = reportReader.GetInt32(1);
        var approved = reportReader.GetInt32(2);
        var rejected = reportReader.GetInt32(3);
        var amount = reportReader.GetDouble(4);
        await reportReader.DisposeAsync();

        var decided = approved + rejected;
        var approvalRate = decided == 0 ? 0 : Math.Round(100.0 * approved / decided, 0, MidpointRounding.AwayFromZero);

        await using var rangeChecks = connection.CreateCommand();
        rangeChecks.CommandText = """
            SELECT COUNT(*),
                   COALESCE(SUM(CASE WHEN Verdict = 'HIGH' THEN 1 ELSE 0 END), 0)
            FROM CheckLogs
            WHERE ($from = '' OR substr(CreatedAt, 1, 10) >= $from)
              AND ($to = '' OR substr(CreatedAt, 1, 10) <= $to);
            """;
        BindWindow(rangeChecks, fromDate, toDate);
        await using var rangeReader = await rangeChecks.ExecuteReaderAsync(cancellationToken);
        await rangeReader.ReadAsync(cancellationToken);
        var checksInRange = rangeReader.GetInt32(0);
        var highInRange = rangeReader.GetInt32(1);
        await rangeReader.DisposeAsync();
        var highPct = checksInRange == 0
            ? 0
            : Math.Round(100.0 * highInRange / checksInRange, 1, MidpointRounding.AwayFromZero);

        await using var monthChecks = connection.CreateCommand();
        monthChecks.CommandText = """
            SELECT COUNT(*),
                   COALESCE(SUM(CASE WHEN Verdict = 'HIGH' THEN 1 ELSE 0 END), 0)
            FROM CheckLogs
            WHERE substr(CreatedAt, 1, 7) = $month;
            """;
        monthChecks.Parameters.AddWithValue("$month", currentMonth);
        await using var monthReader = await monthChecks.ExecuteReaderAsync(cancellationToken);
        await monthReader.ReadAsync(cancellationToken);
        var checksThisMonth = monthReader.GetInt32(0);
        var highThisMonth = monthReader.GetInt32(1);
        await monthReader.DisposeAsync();

        await using var companies = connection.CreateCommand();
        companies.CommandText = "SELECT COUNT(*) FROM Companies;";
        var companyCount = Convert.ToInt32(await companies.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);

        await using var postings = connection.CreateCommand();
        postings.CommandText = $"""
            SELECT COUNT(*) FROM JobPostings
            WHERE ($from = '' OR PostedDate >= $from) AND ($to = '' OR PostedDate <= $to);
            """;
        BindWindow(postings, fromDate, toDate);
        var postingCount = Convert.ToInt32(await postings.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);

        await using var ratings = connection.CreateCommand();
        ratings.CommandText = $"""
            SELECT AVG(Rating) FROM InterviewReviews
            WHERE ($from = '' OR ReviewDate >= $from) AND ($to = '' OR ReviewDate <= $to);
            """;
        BindWindow(ratings, fromDate, toDate);
        var ratingValue = await ratings.ExecuteScalarAsync(cancellationToken);
        double? average = ratingValue is null or DBNull ? null : Math.Round(Convert.ToDouble(ratingValue), 1, MidpointRounding.AwayFromZero);

        var kpis = new DashboardKpis(total, pending, approved, approvalRate, amount, checksThisMonth, highPct, companyCount, postingCount, average);
        return (kpis, highThisMonth);
    }

    public async Task<IReadOnlyList<MonthSeries>> ReportsPerMonthAsync(string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT substr(ReportedDate, 1, 7),
                   COUNT(*),
                   COALESCE(SUM(CASE WHEN Status = 'Approved' THEN 1 ELSE 0 END), 0)
            FROM ScamReports
            WHERE {ReportWindow}
            GROUP BY substr(ReportedDate, 1, 7)
            ORDER BY substr(ReportedDate, 1, 7)
            LIMIT 36;
            """;
        BindWindow(command, fromDate, toDate);
        var rows = new List<MonthSeries>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MonthSeries(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CategoryCount>> CountsByAsync(
        string column,
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken)
    {
        if (column is not ("ScamType" or "Status" or "Channel"))
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {column}, COUNT(*)
            FROM ScamReports
            WHERE {ReportWindow}
            GROUP BY {column}
            ORDER BY COUNT(*) DESC, {column}
            LIMIT 20;
            """;
        BindWindow(command, fromDate, toDate);
        var rows = new List<CategoryCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CategoryCount(reader.GetString(0), reader.GetInt32(1)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CompanyRanking>> TopApprovedCompaniesAsync(
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT c.Name, c.OfficialDomain, c.Industry, COUNT(*), COALESCE(SUM(r.AmountRequestedUsd), 0)
            FROM Companies c
            INNER JOIN ScamReports r ON r.CompanyId = c.CompanyId
            WHERE r.Status = 'Approved'
              AND ($from = '' OR r.ReportedDate >= $from)
              AND ($to = '' OR r.ReportedDate <= $to)
            GROUP BY c.CompanyId, c.Name, c.OfficialDomain, c.Industry
            ORDER BY COUNT(*) DESC, COALESCE(SUM(r.AmountRequestedUsd), 0) DESC, c.Name
            LIMIT 7;
            """;
        BindWindow(command, fromDate, toDate);
        var rows = new List<CompanyRanking>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CompanyRanking(
                rows.Count + 1,
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetDouble(4)));
        }

        return rows;
    }

    public async Task<CompanyRanking?> HighestApprovedAmountAsync(
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.Name, c.OfficialDomain, c.Industry, COUNT(*), COALESCE(SUM(r.AmountRequestedUsd), 0)
            FROM Companies c
            INNER JOIN ScamReports r ON r.CompanyId = c.CompanyId
            WHERE r.Status = 'Approved'
              AND ($from = '' OR r.ReportedDate >= $from)
              AND ($to = '' OR r.ReportedDate <= $to)
            GROUP BY c.CompanyId, c.Name, c.OfficialDomain, c.Industry
            ORDER BY COALESCE(SUM(r.AmountRequestedUsd), 0) DESC, COUNT(*) DESC, c.Name
            LIMIT 1;
            """;
        BindWindow(command, fromDate, toDate);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CompanyRanking(1, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetDouble(4));
    }

    public async Task<IReadOnlyList<CheckMonth>> ChecksByMonthAsync(string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT substr(CreatedAt, 1, 7),
                   COALESCE(SUM(CASE WHEN Verdict = 'LOW' THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN Verdict = 'MEDIUM' THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN Verdict = 'HIGH' THEN 1 ELSE 0 END), 0)
            FROM CheckLogs
            WHERE ($from = '' OR substr(CreatedAt, 1, 10) >= $from)
              AND ($to = '' OR substr(CreatedAt, 1, 10) <= $to)
            GROUP BY substr(CreatedAt, 1, 7)
            ORDER BY substr(CreatedAt, 1, 7)
            LIMIT 36;
            """;
        BindWindow(command, fromDate, toDate);
        var rows = new List<CheckMonth>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CheckMonth(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CompanyCompare>> PostingsVsReportsAsync(
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.Name,
                   (SELECT COUNT(*) FROM JobPostings p
                    WHERE p.CompanyId = c.CompanyId
                      AND ($from = '' OR p.PostedDate >= $from)
                      AND ($to = '' OR p.PostedDate <= $to)),
                   (SELECT COUNT(*) FROM ScamReports r
                    WHERE r.CompanyId = c.CompanyId AND r.Status = 'Approved'
                      AND ($from = '' OR r.ReportedDate >= $from)
                      AND ($to = '' OR r.ReportedDate <= $to)),
                   (SELECT AVG(i.Rating) FROM InterviewReviews i
                    WHERE i.CompanyId = c.CompanyId
                      AND ($from = '' OR i.ReviewDate >= $from)
                      AND ($to = '' OR i.ReviewDate <= $to))
            FROM Companies c
            ORDER BY 3 DESC, c.Name
            LIMIT 20;
            """;
        BindWindow(command, fromDate, toDate);
        var rows = new List<CompanyCompare>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            double? rating = reader.IsDBNull(3) ? null : Math.Round(reader.GetDouble(3), 1, MidpointRounding.AwayFromZero);
            rows.Add(new CompanyCompare(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), rating));
        }

        return rows;
    }

    private const string ReportWindow = """
        ($from = '' OR ReportedDate >= $from) AND ($to = '' OR ReportedDate <= $to)
        """;

    private static void BindWindow(Microsoft.Data.Sqlite.SqliteCommand command, string? fromDate, string? toDate)
    {
        command.Parameters.AddWithValue("$from", fromDate ?? "");
        command.Parameters.AddWithValue("$to", toDate ?? "");
    }
}
