using JobLensChat.Models;

namespace JobLensChat.Data;

public sealed partial class CompanyRepository
{
    public async Task<CompanyActivity> GetActivityAsync(int companyId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM ScamReports WHERE CompanyId = $id AND Status = 'Approved'),
                (SELECT COUNT(*) FROM ScamReports WHERE CompanyId = $id AND Status = 'Pending'),
                (SELECT COUNT(*) FROM JobPostings WHERE CompanyId = $id),
                (SELECT COUNT(*) FROM InterviewReviews WHERE CompanyId = $id),
                (SELECT AVG(Rating) FROM InterviewReviews WHERE CompanyId = $id);
            """;
        command.Parameters.AddWithValue("$id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        double? average = reader.IsDBNull(4) ? null : Math.Round(reader.GetDouble(4), 2, MidpointRounding.AwayFromZero);
        return new CompanyActivity(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), average);
    }
}
