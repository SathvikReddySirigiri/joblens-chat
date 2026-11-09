using System.Net;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace JobLens.Tests;

public sealed class HealthzTests : IClassFixture<HealthzTests.Factory>
{
    private readonly Factory factory;

    public HealthzTests(Factory factory) => this.factory = factory;

    [Fact]
    public async Task Healthz_returns_ok()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/healthz");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", body);
    }

    [Fact]
    public async Task Healthz_works_without_X_Api_Key()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        // Factory configures Mcp:AccessKey; healthz must stay public.
        Assert.False(client.DefaultRequestHeaders.Contains(McpAccessKeyMiddleware.HeaderName));

        using var response = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "joblens-healthz-" + Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(root);
            var dbPath = Path.Combine(root, "joblens.db");
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Path"] = dbPath,
                    ["Mcp:AccessKey"] = "healthz-test-access-key",
                    ["AllowedHosts"] = "localhost",
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Temp DB may still be briefly locked on Windows.
            }
        }
    }
}
