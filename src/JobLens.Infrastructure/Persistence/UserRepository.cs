using JobLens.Domain.Entities;
using JobLens.Application.Dtos;
using Microsoft.Data.Sqlite;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration, IWebHostEnvironment environment)
        : this(SqliteConnections.ForApp(configuration, environment))
    {
    }

    internal UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UserId, Email, DisplayName, PasswordHash, Role, CreatedAt
            FROM Users
            WHERE Email = $email COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$email", email.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UserAccount(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5));
    }

    public async Task<int> CreateAsync(UserAccount user, CancellationToken cancellationToken)
    {
        await using var connection = await SqliteConnections.OpenAsync(_connectionString, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Email, DisplayName, PasswordHash, Role, CreatedAt)
            VALUES ($email, $name, $hash, $role, $createdAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$name", user.DisplayName);
        command.Parameters.AddWithValue("$hash", user.PasswordHash);
        command.Parameters.AddWithValue("$role", user.Role);
        command.Parameters.AddWithValue("$createdAt", user.CreatedAt);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }
}
