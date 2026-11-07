using JobLens.Application.Dtos;
using JobLens.Domain.Entities;
using JobLens.Infrastructure.Persistence;
using JobLens.Infrastructure.Security;

namespace JobLens.Tests;

internal static class TestActors
{
    public static async Task<ActingUser> CreateModeratorAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var id = await InsertAsync(connectionString, "moderator@test.local", "Test Moderator", ActingUser.ModeratorRole, cancellationToken);
        return ActingUser.Moderator(id);
    }

    public static async Task<ActingUser> CreateStudentAsync(
        string connectionString,
        string email = "student@test.local",
        CancellationToken cancellationToken = default)
    {
        var id = await InsertAsync(connectionString, email, "Test Student", ActingUser.StudentRole, cancellationToken);
        return ActingUser.Student(id);
    }

    private static async Task<int> InsertAsync(
        string connectionString,
        string email,
        string displayName,
        string role,
        CancellationToken cancellationToken)
    {
        var users = new UserRepository(connectionString);
        return await users.CreateAsync(
            new UserAccount(0, email, displayName, UserPassword.Hash(email, "TestPass#2026"), role, "2026-01-01"),
            cancellationToken);
    }
}
