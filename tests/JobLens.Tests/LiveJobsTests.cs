using System.Net;
using System.Text;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Application.Services;
using JobLens.Infrastructure.External;
using JobLens.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class LiveJobsTests
{
    [Fact]
    public void Parser_reads_fixture_and_strips_html_snippets()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "muse-jobs-sample.json"));
        var parsed = MuseJobsClient.ParseJson(json, requestedPage: 1);

        Assert.Equal(3, parsed.Items.Count);
        Assert.Equal(1, parsed.Page);
        Assert.Equal(3, parsed.PageCount);
        Assert.Equal("Data Intern", parsed.Items[0].Title);
        Assert.Equal("Amazon Inc", parsed.Items[0].CompanyName);
        Assert.Equal("Tampa, FL", parsed.Items[0].Locations);
        Assert.DoesNotContain("<", parsed.Items[0].DescriptionSnippet, StringComparison.Ordinal);
        Assert.Contains("data", parsed.Items[0].DescriptionSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("APIs & services", parsed.Items[1].DescriptionSnippet, StringComparison.Ordinal);
        Assert.Equal("The Muse", parsed.Items[0].Source);
        Assert.True(parsed.Items[0].DescriptionSnippet.Length <= 300);
    }

    [Fact]
    public void Keyword_filter_matches_title_case_insensitively()
    {
        var jobs = new List<LiveJob>
        {
            new("1", "Data Intern", "Amazon", "Tampa, FL", "Internship", "Data Science", "x", null, "https://example.com/1"),
            new("2", "Software Engineer", "Meta", "Remote", "Mid Level", "Software Engineering", "y", null, "https://example.com/2"),
        };

        var filtered = LiveJobsService.FilterByKeyword(jobs, "intern");
        Assert.Single(filtered);
        Assert.Equal("Data Intern", filtered[0].Title);
    }

    [Fact]
    public async Task Trust_matches_company_suffixes_and_counts_approved_reports()
    {
        await using var database = await TempDatabase.CreateAsync();
        var companies = new CompanyRepository(database.ConnectionString);
        var reports = new ScamReportRepository(database.ConnectionString);
        var recruiters = new RecruiterRepository(database.ConnectionString);
        var companyService = new CompanyService(companies);
        var reportService = new ScamReportService(reports, companies, recruiters);
        var moderator = await TestActors.CreateModeratorAsync(database.ConnectionString);
        var student = await TestActors.CreateStudentAsync(database.ConnectionString);

        var amazon = await companyService.CreateAsync(new CompanyFormVm
        {
            Name = "Amazon",
            OfficialDomain = "amazon.com",
            Industry = "Technology",
            HqState = "WA",
        }, moderator, CancellationToken.None);
        Assert.True(amazon.Success);

        var created = await reportService.CreateAsync(new ScamReportFormVm
        {
            CompanyId = amazon.Id!.Value,
            RecruiterEmail = "hr@amaz0n-careers.net",
            ScamType = "Fake check",
            Channel = "Email",
            AmountRequestedUsd = 100,
            DomainAgeDays = 10,
            Description = "Fake Amazon check scam report used for live-jobs trust tests.",
        }, student, CancellationToken.None);
        Assert.True((await reportService.ApproveAsync(created.Id!.Value, moderator, CancellationToken.None)).Success);

        var service = new LiveJobsService(new StubLiveJobsProvider([]), companies);
        var all = await companies.GetAllAsync(CancellationToken.None);
        var (matched, companyId) = await service.BuildTrustAsync("Amazon Inc", all, CancellationToken.None);
        Assert.Equal(amazon.Id, companyId);
        Assert.Equal("Amazon", matched.MatchedCompany);
        Assert.Equal("amazon.com", matched.OfficialDomain);
        Assert.Equal(1, matched.ApprovedScamReports);
        Assert.Contains(matched.Notes, note => note.Contains("@amazon.com", StringComparison.Ordinal));
        Assert.Contains(matched.Notes, note => note.Contains("1 approved scam reports", StringComparison.Ordinal));

        var (unmatched, unmatchedId) = await service.BuildTrustAsync("Totally Unknown Co", all, CancellationToken.None);
        Assert.Null(unmatchedId);
        Assert.Null(unmatched.MatchedCompany);
        Assert.Equal(0, unmatched.ApprovedScamReports);
        Assert.Contains(unmatched.Notes, note => note.Contains("Not in the JobLens company list", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Http_400_returns_unsupported_filter_message()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 32 });
        var client = CreateClient(new StubHandler(HttpStatusCode.BadRequest, "{\"error\":\"bad category\"}"), cache, apiKey: null);
        var result = await client.SearchAsync(new LiveJobSearch(Category: "Software Engineering", Level: "Internship", Page: 1), CancellationToken.None);
        Assert.Equal(MuseJobsClient.UnsupportedFilterMessage, result.Message);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Http_429_returns_busy_message_and_prefers_cached_results()
    {
        var search = new LiveJobSearch(Level: "Internship", Page: 1);
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 32 });
        var handler = new StubHandler(HttpStatusCode.TooManyRequests, "{}");
        var client = CreateClient(handler, cache, apiKey: null);

        var busy = await client.SearchAsync(search, CancellationToken.None);
        Assert.Equal(MuseJobsClient.BusyMessage, busy.Message);
        Assert.Empty(busy.Items);
        Assert.False(busy.FromCache);

        cache.Set(
            "muse|1||Internship|||",
            new LiveJobResult(
                [new LiveJob("9", "Cached Intern", "Amazon", "Remote", "Internship", "IT", "snippet", null, "https://example.com/9")],
                1,
                1,
                null,
                false),
            new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });

        var cached = await client.SearchAsync(search, CancellationToken.None);
        Assert.True(cached.FromCache);
        Assert.Equal("Cached Intern", cached.Items[0].Title);
    }

    [Fact]
    public void Built_url_uses_1_based_page_skips_empty_params_and_api_key_only_when_configured()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 32 });
        var withoutKey = CreateClient(new StubHandler(HttpStatusCode.OK, "{\"page\":1,\"page_count\":1,\"results\":[]}"), cache, apiKey: null);
        var urlWithout = withoutKey.BuildUrl(new LiveJobSearch(Level: "Internship", Page: 0));
        Assert.False(MuseJobsClient.IncludesApiKey(urlWithout));
        Assert.Contains("page=1", urlWithout, StringComparison.Ordinal);
        Assert.DoesNotContain("page=0", urlWithout, StringComparison.Ordinal);
        Assert.DoesNotContain("category=", urlWithout, StringComparison.Ordinal);
        Assert.DoesNotContain("location=", urlWithout, StringComparison.Ordinal);
        Assert.DoesNotContain("company=", urlWithout, StringComparison.Ordinal);
        Assert.Contains("level=Internship", urlWithout, StringComparison.Ordinal);

        var withKey = CreateClient(new StubHandler(HttpStatusCode.OK, "{\"page\":1,\"page_count\":1,\"results\":[]}"), cache, apiKey: "demo-key");
        var urlWith = withKey.BuildUrl(new LiveJobSearch(
            Category: "Software Engineering",
            Level: "Internship",
            Location: "Tampa, FL",
            CompanyName: "  ",
            Page: 2));
        Assert.True(MuseJobsClient.IncludesApiKey(urlWith));
        Assert.Contains("page=2", urlWith, StringComparison.Ordinal);
        Assert.Contains("category=Software%20Engineering", urlWith, StringComparison.Ordinal);
        Assert.Contains("location=Tampa%2C%20FL", urlWith, StringComparison.Ordinal);
        Assert.DoesNotContain("company=", urlWith, StringComparison.Ordinal);
        Assert.Contains("api_key=demo-key", urlWith, StringComparison.Ordinal);

        var clamped = withoutKey.BuildUrl(new LiveJobSearch(Level: "Internship", Page: 99));
        Assert.Contains("page=5", clamped, StringComparison.Ordinal);
    }

    private static MuseJobsClient CreateClient(HttpMessageHandler handler, IMemoryCache cache, string? apiKey)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.themuse.com/api/public/"),
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jobs:TheMuse:ApiKey"] = apiKey,
            })
            .Build();
        return new MuseJobsClient(
            http,
            cache,
            config,
            new UsaJobsTests.FakeHostEnvironment(),
            NullLogger<MuseJobsClient>.Instance);
    }

    private sealed class StubLiveJobsProvider(IReadOnlyList<LiveJob> items) : ILiveJobsProvider
    {
        public string Name => "The Muse";
        public string AttributionText => "test";
        public string AttributionUrl => "https://www.themuse.com";

        public Task<LiveJobsResult> SearchAsync(LiveJobsQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new LiveJobsResult(items, query.Page, 1, null, false));
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
}
