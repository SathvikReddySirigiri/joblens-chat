using System.Net;
using System.Net.Http;
using System.Text;
using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Infrastructure;
using JobLens.Infrastructure.External;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class UsaJobsTests
{
    [Fact]
    public void Parser_reads_fixture_including_item_missing_pay_and_location()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "usajobs-sample.json"));
        var parsed = UsaJobsClient.ParseJson(json, requestedPage: 1);

        Assert.Equal(2, parsed.Items.Count);
        Assert.Equal(1, parsed.Page);
        Assert.Equal(2, parsed.PageCount);

        var first = parsed.Items[0];
        Assert.Equal("Data Scientist Intern", first.Title);
        Assert.Contains("Veterans Affairs", first.CompanyName, StringComparison.Ordinal);
        Assert.Equal("Tampa, Florida", first.Locations);
        Assert.Equal("GS-7–9", first.Levels);
        Assert.Equal("Data Science", first.Categories);
        Assert.Contains("$45,000", first.PayRange, StringComparison.Ordinal);
        Assert.Contains("per year", first.PayRange, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<", first.DescriptionSnippet, StringComparison.Ordinal);
        Assert.Contains("health", first.DescriptionSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("USAJOBS", first.Source);
        Assert.False(first.IsRemote);

        var second = parsed.Items[1];
        Assert.Equal("IT Specialist (Recent Grad)", second.Title);
        Assert.True(string.IsNullOrWhiteSpace(second.Locations));
        Assert.True(string.IsNullOrWhiteSpace(second.PayRange));
        Assert.True(second.IsRemote);
        Assert.Equal("GS-5–7", second.Levels);
    }

    [Fact]
    public void Built_url_includes_hiring_path_category_page_and_skips_empty_params()
    {
        var client = CreateClient(HttpStatusCode.OK, "{}");
        var url = client.BuildUrl(new LiveJobsQuery(
            Keyword: "data",
            Location: "Tampa, FL",
            CategoryCode: "1560",
            HiringPath: "student",
            RemoteOnly: true,
            Page: 0));

        Assert.Contains("Page=1", url, StringComparison.Ordinal);
        Assert.DoesNotContain("Page=0", url, StringComparison.Ordinal);
        Assert.Contains("HiringPath=student", url, StringComparison.Ordinal);
        Assert.Contains("JobCategoryCode=1560", url, StringComparison.Ordinal);
        Assert.Contains("Keyword=data", url, StringComparison.Ordinal);
        Assert.Contains("LocationName=Tampa%2C%20FL", url, StringComparison.Ordinal);
        Assert.Contains("RemoteIndicator=True", url, StringComparison.Ordinal);
        Assert.Contains("ResultsPerPage=20", url, StringComparison.Ordinal);
        Assert.Contains("SortField=OpenDate", url, StringComparison.Ordinal);
        Assert.DoesNotContain("Organization=", url, StringComparison.Ordinal);

        var sparse = client.BuildUrl(new LiveJobsQuery(HiringPath: "graduates", Page: 2));
        Assert.Contains("Page=2", sparse, StringComparison.Ordinal);
        Assert.Contains("HiringPath=graduates", sparse, StringComparison.Ordinal);
        Assert.DoesNotContain("Keyword=", sparse, StringComparison.Ordinal);
        Assert.DoesNotContain("LocationName=", sparse, StringComparison.Ordinal);
        Assert.DoesNotContain("JobCategoryCode=", sparse, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoteIndicator=", sparse, StringComparison.Ordinal);
    }

    [Fact]
    public void Request_headers_come_from_config_values()
    {
        using var request = UsaJobsClient.CreateRequest(
            "search?Page=1",
            userAgent: "tester@example.edu",
            apiKey: "test-key-not-secret");

        Assert.Equal("data.usajobs.gov", request.Headers.Host);
        Assert.True(request.Headers.TryGetValues("User-Agent", out var agents));
        Assert.Contains("tester@example.edu", agents);
        Assert.True(request.Headers.TryGetValues("Authorization-Key", out var keys));
        Assert.Contains("test-key-not-secret", keys);
    }

    [Fact]
    public void Provider_selection_defaults_to_usajobs_and_honors_themuse()
    {
        var defaultConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        Assert.Equal(typeof(UsaJobsClient), DependencyInjection.ResolveLiveJobsProviderType(defaultConfig));

        var usa = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jobs:Provider"] = "UsaJobs" })
            .Build();
        Assert.Equal(typeof(UsaJobsClient), DependencyInjection.ResolveLiveJobsProviderType(usa));

        var muse = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jobs:Provider"] = "TheMuse" })
            .Build();
        Assert.Equal(typeof(MuseJobsClient), DependencyInjection.ResolveLiveJobsProviderType(muse));
    }

    [Fact]
    public void Non_usajobs_apply_url_is_hidden()
    {
        Assert.Null(LiveJobsService.SafeApplyUrl("https://evil.example/apply", "USAJOBS"));
        Assert.Null(LiveJobsService.SafeApplyUrl("https://themuse.com/jobs/1", "USAJOBS"));
        Assert.Equal(
            "https://www.usajobs.gov/GetJob/ViewDetails/1",
            LiveJobsService.SafeApplyUrl("https://www.usajobs.gov/GetJob/ViewDetails/1", "USAJOBS"));
        Assert.Equal(
            "https://www.themuse.com/jobs/1",
            LiveJobsService.SafeApplyUrl("https://www.themuse.com/jobs/1", "The Muse"));
    }

    [Fact]
    public async Task Missing_credentials_returns_friendly_message_without_caching()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 32 });
        var client = CreateClient(
            HttpStatusCode.OK,
            "{}",
            apiKey: null,
            userAgent: null);
        var result = await client.SearchAsync(new LiveJobsQuery(HiringPath: "student"), CancellationToken.None);
        Assert.Equal(UsaJobsClient.MissingCredentialsMessage, result.Message);
        Assert.Empty(result.Items);
        Assert.False(result.FromCache);
    }

    private static UsaJobsClient CreateClient(
        HttpStatusCode status,
        string body,
        string? apiKey = "test-key",
        string? userAgent = "tester@example.edu")
    {
        var http = new HttpClient(new StubHandler(status, body))
        {
            BaseAddress = new Uri("https://data.usajobs.gov/api/"),
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jobs:UsaJobs:ApiKey"] = apiKey,
                ["Jobs:UsaJobs:UserAgent"] = userAgent,
            })
            .Build();
        return new UsaJobsClient(
            http,
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 32 }),
            config,
            new FakeHostEnvironment(),
            NullLogger<UsaJobsClient>.Instance);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    internal sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "JobLens.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
