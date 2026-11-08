using JobLens.Infrastructure.Persistence;
using JobLens.Infrastructure.Security;
using JobLens.Domain.Entities;
using JobLens.Application.Services;
using JobLens.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class RecruiterAndPasswordTests
{
    [Fact]
    public async Task Recruiter_email_is_unique_and_domain_is_extracted()
    {
        await using var database = await TempDatabase.CreateAsync();
        var repository = new RecruiterRepository(database.ConnectionString);
        var service = new RecruiterService(repository);

        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var created = await service.CreateAsync(new RecruiterFormVm
        {
            Email = "HR@Amazon.com",
            FirstSeen = "2026-02-01",
            Notes = "Campus contact",
        }, moderator, CancellationToken.None);

        Assert.True(created.Success);
        var saved = await repository.GetByIdAsync(created.Id!.Value, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("hr@amazon.com", saved.Email);
        Assert.Equal("amazon.com", saved.Domain);

        var duplicate = await service.CreateAsync(new RecruiterFormVm
        {
            Email = "hr@amazon.com",
        }, moderator, CancellationToken.None);

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

    [Fact]
    public void Development_falls_back_to_documented_defaults_when_seed_keys_missing()
    {
        var (moderator, student) = DatabaseInitializer.DemoPasswords(
            EmptyConfig(),
            Env(Environments.Development),
            NullLogger.Instance);

        Assert.Equal("Moderator#2026", moderator);
        Assert.Equal("Student#2026", student);
    }

    [Fact]
    public void NonDevelopment_throws_when_seed_passwords_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DatabaseInitializer.DemoPasswords(
                EmptyConfig(),
                Env(Environments.Production),
                NullLogger.Instance));

        Assert.Equal(
            "Set Seed__ModeratorPassword and Seed__StudentPassword in App Settings",
            ex.Message);
    }

    [Fact]
    public void NonDevelopment_uses_configured_seed_passwords()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:ModeratorPassword"] = "ProdMod#1",
                ["Seed:StudentPassword"] = "ProdStu#1",
            })
            .Build();

        var (moderator, student) = DatabaseInitializer.DemoPasswords(
            config,
            Env(Environments.Staging),
            NullLogger.Instance);

        Assert.Equal("ProdMod#1", moderator);
        Assert.Equal("ProdStu#1", student);
    }

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private static StubEnvironment Env(string name) => new() { EnvironmentName = name };

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "JobLens.Tests";
        public string WebRootPath { get; set; } = "";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
