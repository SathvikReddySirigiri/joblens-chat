using JobLensChat.Data;
using JobLensChat.Models;
using JobLensChat.Services;
using JobLensChat.ViewModels;
using Xunit;

namespace JobLensChat.Tests;

public sealed class RecruiterAndPasswordTests
{
    [Fact]
    public async Task Recruiter_email_is_unique_and_domain_is_extracted()
    {
        await using var database = await TempDatabase.CreateAsync();
        var repository = new RecruiterRepository(database.ConnectionString);
        var service = new RecruiterService(repository);

        var created = await service.CreateAsync(new RecruiterFormVm
        {
            Email = "HR@Amazon.com",
            FirstSeen = "2026-02-01",
            Notes = "Campus contact",
        }, CancellationToken.None);

        Assert.True(created.Success);
        var saved = await repository.GetByIdAsync(created.Id!.Value, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("hr@amazon.com", saved.Email);
        Assert.Equal("amazon.com", saved.Domain);

        var duplicate = await service.CreateAsync(new RecruiterFormVm
        {
            Email = "hr@amazon.com",
        }, CancellationToken.None);

        Assert.False(duplicate.Success);
        Assert.Equal("A recruiter with this email already exists.", duplicate.ErrorMessage);
    }

    [Fact]
    public async Task Password_hash_verifies_and_is_not_the_password()
    {
        const string email = "student1@joblens.demo";
        const string password = "Student#2026";
        var hash = UserPassword.Hash(email, password);

        Assert.True(UserPassword.Verify(email, hash, password));
        Assert.False(UserPassword.Verify(email, hash, "Student#2025"));
        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);

        await using var database = await TempDatabase.CreateAsync();
        var users = new UserRepository(database.ConnectionString);
        await users.CreateAsync(
            new UserAccount(0, email, "Student 1", hash, "Student", "2026-01-01"),
            CancellationToken.None);
        var stored = await users.GetByEmailAsync(email, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.True(UserPassword.Verify(stored.Email, stored.PasswordHash, password));
        Assert.DoesNotContain(password, stored.PasswordHash, StringComparison.Ordinal);
    }
}
