using JobLens.Domain.Entities;
using JobLens.Application.Dtos;
using Microsoft.Data.Sqlite;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

public sealed partial class CompanyRepository : ICompanyRepository
{
    private readonly string _connectionString;

    public CompanyRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal CompanyRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CompanyId, Name, OfficialDomain, Industry, HqState
            FROM Companies
            ORDER BY Name
            LIMIT 50;
            """;
        var companies = new List<Company>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            companies.Add(ReadCompany(reader));
        }

        return companies;
    }

    public async Task<Company?> GetByIdAsync(int companyId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CompanyId, Name, OfficialDomain, Industry, HqState
            FROM Companies
            WHERE CompanyId = $id;
            """;
        command.Parameters.AddWithValue("$id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCompany(reader) : null;
    }

    public async Task<int> CreateAsync(
        string name,
        string officialDomain,
        string industry,
        string hqState,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Companies (Name, OfficialDomain, Industry, HqState)
            VALUES ($name, $domain, $industry, $state);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$domain", officialDomain);
        command.Parameters.AddWithValue("$industry", industry);
        command.Parameters.AddWithValue("$state", hqState);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> UpdateAsync(
        int companyId,
        string name,
        string officialDomain,
        string industry,
        string hqState,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Companies
            SET Name = $name, OfficialDomain = $domain, Industry = $industry, HqState = $state
            WHERE CompanyId = $id;
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$domain", officialDomain);
        command.Parameters.AddWithValue("$industry", industry);
        command.Parameters.AddWithValue("$state", hqState);
        command.Parameters.AddWithValue("$id", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> DeleteAsync(int companyId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Companies WHERE CompanyId = $id;";
        command.Parameters.AddWithValue("$id", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<int> CountDependentsAsync(int companyId, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM ScamReports WHERE CompanyId = $id)
                + (SELECT COUNT(*) FROM JobPostings WHERE CompanyId = $id)
                + (SELECT COUNT(*) FROM InterviewReviews WHERE CompanyId = $id);
            """;
        command.Parameters.AddWithValue("$id", companyId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken) =>
        await ExistsAsync("Name", name, excludeId, cancellationToken);

    public async Task<bool> ExistsByDomainAsync(string domain, int? excludeId, CancellationToken cancellationToken) =>
        await ExistsAsync("OfficialDomain", domain, excludeId, cancellationToken);

    private async Task<bool> ExistsAsync(string column, string value, int? excludeId, CancellationToken cancellationToken)
    {
        // Column is one of two fixed identifiers, never a request value.
        if (column is not ("Name" or "OfficialDomain"))
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(*)
            FROM Companies
            WHERE {column} = $value COLLATE NOCASE
              AND ($excludeId = 0 OR CompanyId <> $excludeId);
            """;
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$excludeId", excludeId ?? 0);
        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static Company ReadCompany(SqliteDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4));
}
