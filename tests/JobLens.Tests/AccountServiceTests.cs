using JobLens.Application.Services;
using JobLens.Domain.Entities;
using JobLens.Infrastructure.Persistence;
using JobLens.Infrastructure.Security;
using Xunit;

namespace JobLens.Tests;

public sealed class AccountServiceTests
{
    [Fact]
    public async Task SignIn_succeeds_for_correct_credentials()
    {
        await using var database = await TempDatabase.CreateAsync();
        var users = new UserRepository(database.ConnectionString);
        var hasher = new AspNetPasswordHasher();
        const string email = "student1@joblens.demo";
        const string secret = "Student#2026";
        await users.CreateAsync(
            new UserAccount(0, email, "Student 1", hasher.Hash(email, secret), "Student", "2026-01-01"),
            CancellationToken.None);

        var service = new AccountService(users, hasher);
        var result = await service.SignInCheckAsync(email, secret, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(email, result.Email);
        Assert.Equal("Student", result.Role);
        Assert.Equal("Student 1", result.DisplayName);
        Assert.NotNull(result.UserId);
    }

    [Fact]
    public async Task SignIn_fails_with_generic_message_for_wrong_password()
    {
        await using var database = await TempDatabase.CreateAsync();
        var users = new UserRepository(database.ConnectionString);
        var hasher = new AspNetPasswordHasher();
        const string email = "student1@joblens.demo";
        await users.CreateAsync(
            new UserAccount(0, email, "Student 1", hasher.Hash(email, "Student#2026"), "Student", "2026-01-01"),
            CancellationToken.None);

        var service = new AccountService(users, hasher);
        var result = await service.SignInCheckAsync(email, "WrongPassword1", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid email or password.", result.Error);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task SignIn_fails_with_generic_message_for_unknown_email()
    {
        await using var database = await TempDatabase.CreateAsync();
        var service = new AccountService(new UserRepository(database.ConnectionString), new AspNetPasswordHasher());

        var result = await service.SignInCheckAsync("nobody@joblens.demo", "Student#2026", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task Register_creates_student_when_rules_pass()
    {
        await using var database = await TempDatabase.CreateAsync();
        var users = new UserRepository(database.ConnectionString);
        var hasher = new AspNetPasswordHasher();
        var service = new AccountService(users, hasher);

        var created = await service.RegisterStudentAsync(
            "New.Student@Example.EDU",
            "New Student",
            "SecurePass1",
            CancellationToken.None);

        Assert.True(created.Success);
        Assert.NotNull(created.Id);

        var stored = await users.GetByIdAsync(created.Id.Value, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("new.student@example.edu", stored.Email);
        Assert.Equal("Student", stored.Role);
        Assert.Equal("New Student", stored.DisplayName);

        var signedIn = await service.SignInCheckAsync("new.student@example.edu", "SecurePass1", CancellationToken.None);
        Assert.True(signedIn.Success);
    }

    [Theory]
    [InlineData("short1Aa", "Password must be at least 10 characters and include a letter and a digit.")]
    [InlineData("abcdefghij", "Password must be at least 10 characters and include a letter and a digit.")]
    [InlineData("1234567890", "Password must be at least 10 characters and include a letter and a digit.")]
    public async Task Register_rejects_weak_secrets(string secret, string expected)
    {
        await using var database = await TempDatabase.CreateAsync();
        var service = new AccountService(new UserRepository(database.ConnectionString), new AspNetPasswordHasher());

        var result = await service.RegisterStudentAsync("ok@joblens.demo", "Ok Student", secret, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        await using var database = await TempDatabase.CreateAsync();
        var users = new UserRepository(database.ConnectionString);
        var hasher = new AspNetPasswordHasher();
        var service = new AccountService(users, hasher);

        var first = await service.RegisterStudentAsync("dup@joblens.demo", "First", "SecurePass1", CancellationToken.None);
        var second = await service.RegisterStudentAsync("DUP@joblens.demo", "Second", "SecurePass2", CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal("An account with this email already exists.", second.ErrorMessage);
    }
}
