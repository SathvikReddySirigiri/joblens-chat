using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Application.Services;
using JobLens.Domain.Skills;
using JobLens.Infrastructure.Documents;
using JobLens.Infrastructure.Persistence;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobLens.Tests;

public sealed class ResumeServiceTests
{
    [Fact]
    public void Privacy_checker_flags_sensitive_patterns_but_allows_phone_and_email()
    {
        var text = """
            Contact: sam@university.edu | (555) 010-2299
            SSN: 000-12-3456
            DOB: 01/15/2004
            Home: 123 Fake Street, Tampa FL
            Passport: X1234567
            Driver license #: D123-4567-8901
            Bank account number: 123456789012
            Age: 22
            Marital status: single
            Photo available upon request
            Skills: SQL and Excel
            """;

        var findings = ResumePrivacyChecker.Check(text);
        Assert.Contains(findings, item => item.Category.Contains("Social Security", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(findings, item => item.Category.Contains("Date of birth", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(findings, item => item.Category.Contains("Home address", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(findings, item => item.Category.Contains("Passport", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(findings, item => item.Category.Contains("Driver license", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(findings, item => item.Category.Contains("Bank", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(findings, item => item.Category.Equals("Age", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(findings, item => item.Category.Contains("Marital", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(findings, item => item.Category.Contains("Photo", StringComparison.OrdinalIgnoreCase));
        Assert.All(findings, item => Assert.Equal(ResumePrivacyChecker.Advice, item.Advice));
        Assert.DoesNotContain(findings, item => item.MaskedLine.Contains("sam@university.edu", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(findings, item => item.Category.Contains("phone", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(findings, item => item.Category.Contains("email", StringComparison.OrdinalIgnoreCase));

        var normal = ResumePrivacyChecker.Check("Sam Lee\nsam@university.edu\n(555) 010-2299\nSQL, Python, Excel");
        Assert.Empty(normal);
    }

    [Fact]
    public void File_signature_check_rejects_renamed_exe()
    {
        var extractor = new OfferTextExtractor();
        // MZ header (Windows executable) with a .pdf extension.
        var bytes = new byte[] { (byte)'M', (byte)'Z', 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        var result = extractor.Extract("malware.pdf", bytes);
        Assert.False(result.Success);
        Assert.Contains("do not match", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Match_score_math_is_matched_over_required()
    {
        var job = "Looking for SQL, Python, and Tableau skills for a data role.";
        var resumeSkills = new[] { "SQL", "Python", "Excel" };
        var match = ResumeMatcher.Match(job, resumeSkills);
        Assert.Equal(3, match.RequiredSkills.Count);
        Assert.Equal(2, match.MatchedSkills.Count);
        Assert.Single(match.MissingSkills);
        Assert.Contains("Tableau", match.MissingSkills);
        Assert.Equal(67, match.ScorePercent);

        var emptyJob = ResumeMatcher.Match("Friendly team in Tampa", resumeSkills);
        Assert.Empty(emptyJob.RequiredSkills);
        Assert.Equal(100, emptyJob.ScorePercent);
    }

    [Fact]
    public async Task Owner_isolation_and_original_file_never_persisted()
    {
        await using var database = await TempDatabase.CreateAsync();
        var root = Path.GetDirectoryName(database.Path)!;
        var service = CreateService(database.ConnectionString);
        var owner = await TestActors.CreateStudentAsync(database.ConnectionString, "resume-owner@test.local");
        var other = await TestActors.CreateStudentAsync(database.ConnectionString, "resume-other@test.local");

        var content = BuildMinimalDocx("""
            Casey Nguyen
            casey@university.edu | (555) 010-1001
            Skills: SQL, Python, Excel, communication
            Built dashboards with Power BI and analyzed data with statistics.
            """);

        var (ok, error, page) = await service.UploadAsync(owner, "casey.docx", content, CancellationToken.None);
        Assert.True(ok, error);
        Assert.NotNull(page?.Resume);
        Assert.Contains("SQL", page!.Skills);

        Assert.Null(await service.GetSummaryForUserAsync(other.UserId, CancellationToken.None));
        Assert.NotNull(await service.GetSummaryAsync(owner, CancellationToken.None));
        Assert.Null(await service.MatchForUserAsync(other, "Need SQL and Python", CancellationToken.None));

        var repo = new ResumeRepository(database.ConnectionString);
        var stored = await repo.GetByUserIdAsync(owner.UserId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.DoesNotContain("PK", stored!.RedactedText, StringComparison.Ordinal); // not raw zip bytes
        Assert.True(stored.RedactedText.Length <= ResumeService.MaxRedactedChars);

        // Original upload must not be written next to the database (only the .db file exists there).
        Assert.False(File.Exists(Path.Combine(root, "casey.docx")));
        Assert.False(Directory.EnumerateFiles(root, "casey.*").Any());
        Assert.DoesNotContain(Directory.EnumerateFiles(root), path =>
            path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

        Assert.True(await service.DeleteAsync(owner, CancellationToken.None));
        Assert.Null(await repo.GetByUserIdAsync(owner.UserId, CancellationToken.None));
    }

    [Fact]
    public void Skill_catalog_has_about_eighty_entries()
    {
        Assert.InRange(SkillCatalog.All.Count, 70, 120);
        Assert.Contains(SkillCatalog.All, skill => skill.Canonical == "SQL");
        Assert.Contains(SkillCatalog.All, skill => skill.Canonical == "Power BI");
    }

    private static ResumeService CreateService(string connectionString) =>
        new(
            new ResumeRepository(connectionString),
            new OfferTextExtractor(),
            new PiiRedactor(),
            new StubAiFactory(),
            NullLogger<ResumeService>.Instance);

    private static byte[] BuildMinimalDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
                   stream,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                   true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                new DocumentFormat.OpenXml.Wordprocessing.Body(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                            new DocumentFormat.OpenXml.Wordprocessing.Text(text)))));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private sealed class StubAiFactory : IAiClientFactory
    {
        public bool IsConfigured => false;

        public string Label => "Demo";

        public IChatClient CreateClient(bool useFunctionInvocation) =>
            throw new NotSupportedException();
    }
}
