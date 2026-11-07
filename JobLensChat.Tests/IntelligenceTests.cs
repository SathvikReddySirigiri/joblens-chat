using JobLensChat.Data;
using JobLensChat.Models;
using JobLensChat.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLensChat.Tests;

public sealed class IntelligenceTests
{
    private readonly PiiRedactor _redactor = new();

    [Fact]
    public void Pii_redactor_hides_ssn_phone_account_and_email_local_part()
    {
        var text = _redactor.Redact("SSN 123-45-6789 and 123456789. Call (813) 555-0142. Account 123456789012. jane.doe@example.com");
        Assert.DoesNotContain("123-45-6789", text);
        Assert.DoesNotContain("123456789", text);
        Assert.DoesNotContain("813", text);
        Assert.DoesNotContain("123456789012", text);
        Assert.Contains("j***@example.com", text);
        Assert.DoesNotContain("jane.doe", text);
    }

    [Fact]
    public void Extractor_rejects_wrong_extension_fake_magic_and_oversize()
    {
        var extractor = new OfferTextExtractor();
        var pdfHeader = "%PDF-1.7 hello"u8.ToArray();
        Assert.False(extractor.Extract("notes.txt", pdfHeader).Success);
        Assert.Equal("Upload a .pdf or .docx file.", extractor.Extract("notes.txt", pdfHeader).Error);

        Assert.False(extractor.Extract("offer.pdf", "not a pdf"u8.ToArray()).Success);
        Assert.Equal("The file contents do not match a PDF or Word document.", extractor.Extract("offer.pdf", "hello"u8.ToArray()).Error);

        var tooBig = new byte[OfferTextExtractor.MaxBytes + 1];
        tooBig[0] = (byte)'%';
        tooBig[1] = (byte)'P';
        tooBig[2] = (byte)'D';
        tooBig[3] = (byte)'F';
        Assert.Equal("The file must be 2 MB or smaller.", extractor.Extract("offer.pdf", tooBig).Error);
    }

    [Fact]
    public void Rule_analyzer_flags_equipment_purchase_and_check_deposit_as_high()
    {
        var analysis = OfferAnalyzerService.AnalyzeByRules(
            "Please purchase equipment and we will reimburse you after training. Deposit the check before your first day.");
        Assert.Equal("HIGH", analysis.Verdict);
        Assert.Contains(analysis.RedFlags, flag => flag.Flag.Contains("Equipment", StringComparison.Ordinal));
        Assert.Contains(analysis.RedFlags, flag => flag.Flag.Contains("Check", StringComparison.Ordinal));
        Assert.Equal("Rule-based analysis (AI unavailable)", analysis.Mode);
    }

    [Fact]
    public void Smart_search_drops_invalid_state_pay_and_unknown_company()
    {
        var companies = new List<Company> { new(1, "Amazon", "amazon.com", "Technology", "WA") };
        var filter = SmartSearchService.Sanitize(
            new SmartSearchService.SearchDraft("intern", "Nope", "Florida", true, true, -5, true),
            "paid remote internships",
            companies);
        Assert.Null(filter.State);
        Assert.Null(filter.MinPayPerHour);
        Assert.Null(filter.CompanyId);
        Assert.True(filter.IsRemote);

        var matched = SmartSearchService.Sanitize(
            new SmartSearchService.SearchDraft("analyst", "amazon", "fl", null, true, 20, true),
            "analyst",
            companies);
        Assert.Equal(1, matched.CompanyId);
        Assert.Equal("FL", matched.State);
        Assert.Equal(20, matched.MinPayPerHour);
        Assert.True(matched.ExcludeCompaniesWithApprovedReports);

        var fallback = SmartSearchService.Sanitize(null, "data analyst intern", companies);
        Assert.Equal("data analyst intern", fallback.Keyword);
        Assert.False(fallback.ExcludeCompaniesWithApprovedReports);
    }

    [Fact]
    public void Edgar_name_normalization_matches_exact_and_close_titles()
    {
        Assert.Equal("amazon", EdgarService.NormalizeName("The Amazon, Inc."));
        var rows = new List<EdgarCompany> { new("AMAZON COM INC", "0001018724", "AMZN") };
        var exact = EdgarService.BestMatch("Amazon", rows);
        Assert.Equal("AMZN", exact!.Ticker);
        Assert.Equal("0001018724", exact.Cik);
        Assert.Equal("AMZN", EdgarService.BestMatch("Amazn", rows)!.Ticker);
        Assert.Null(EdgarService.BestMatch("Totally Unknown Widgets", rows));
    }

    [Fact]
    public void Rdap_adjustment_adds_points_only_for_the_stated_cases()
    {
        var unavailable = RecruiterRiskService.RdapAdjustment(null, false, true, false);
        Assert.Equal(0, unavailable.Points);
        Assert.Equal("Live registry check unavailable", unavailable.Reason!.Text);

        var missing = RecruiterRiskService.RdapAdjustment(SampleRdap(false, null, "No RDAP record found — the domain may not be registered."), false, true, false);
        Assert.Equal(10, missing.Points);

        var young = RecruiterRiskService.RdapAdjustment(SampleRdap(true, 12, "RDAP registration record found."), false, true, false);
        Assert.Equal(20, young.Points);
        var already = RecruiterRiskService.RdapAdjustment(SampleRdap(true, 12, "RDAP registration record found."), true, true, false);
        Assert.Equal(0, already.Points);

        var old = RecruiterRiskService.RdapAdjustment(SampleRdap(true, 4000, "RDAP registration record found.", "1997-09-15"), false, false, false);
        Assert.Equal(0, old.Points);
        Assert.Contains("1997", old.Reason!.Text, StringComparison.Ordinal);
        var official = RecruiterRiskService.RdapAdjustment(SampleRdap(true, 4000, "RDAP registration record found.", "1997-09-15"), false, false, true);
        Assert.Null(official.Reason);
    }

    [Fact]
    public async Task Known_answers_stay_high_high_low_medium_without_live_rdap()
    {
        var root = Path.Combine(Path.GetTempPath(), "joblens-risk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var webRoot = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(webRoot);
        var environment = new RiskHost { ContentRootPath = root, WebRootPath = webRoot };
        var configuration = new ConfigurationBuilder().Build();
        try
        {
            await DatabaseInitializer.InitializeAsync(configuration, environment, NullLogger.Instance);
            var risk = new RecruiterRiskService(new ScamRepository(configuration, environment));
            Assert.Equal("HIGH", (await risk.CheckAsync("hr@amaz0n-careers.net", CancellationToken.None)).Verdict);
            Assert.Equal("HIGH", (await risk.CheckAsync("talent@rnicrosoft-hiring.com", CancellationToken.None)).Verdict);
            Assert.Equal("LOW", (await risk.CheckAsync("recruiting@google.com", CancellationToken.None)).Verdict);
            Assert.Equal("MEDIUM", (await risk.CheckAsync("deloitte.jobs2026@gmail.com", CancellationToken.None)).Verdict);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static RdapResult SampleRdap(bool found, int? age, string message, string? registered = null) => new(
        "example.com",
        found,
        registered,
        null,
        null,
        null,
        age,
        [],
        [],
        message,
        RdapService.SourceLabel,
        DateTimeOffset.UtcNow);

    private sealed class RiskHost : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "JobLensChat.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Development";
    }
}
