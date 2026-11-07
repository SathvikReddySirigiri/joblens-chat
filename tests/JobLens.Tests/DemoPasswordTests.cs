using JobLens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class DemoPasswordTests
{
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
