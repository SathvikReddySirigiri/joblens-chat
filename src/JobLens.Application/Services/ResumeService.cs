using System.Text.Json;
using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Domain.Skills;

namespace JobLens.Application.Services;

public sealed class ResumeService(
    IResumeRepository resumes,
    IOfferTextExtractor extractor,
    PiiRedactor redactor)
{
    public const int MaxRedactedChars = 20_000;
    public const int MaxUploadBytes = IOfferTextExtractor.MaxBytes;

    public async Task<(ResumePageModel Page, string? Error)> GetPageAsync(
        ActingUser actor,
        CancellationToken cancellationToken)
    {
        var resume = await resumes.GetByUserIdAsync(actor.UserId, cancellationToken);
        if (resume is null)
        {
            return (new ResumePageModel(null, [], [], null, null), null);
        }

        var findings = DeserializeFindings(resume.PrivacyFindingsJson);
        var skills = ResumeMatcher.ParseSkillsCsv(resume.SkillsCsv);
        return (new ResumePageModel(
            new ResumeSummary(resume.FileName, resume.PrivacyIssueCount, resume.UploadedAt, skills),
            findings,
            skills,
            null,
            null), null);
    }

    public async Task<(bool Success, string? Error, ResumePageModel? Page)> UploadAsync(
        ActingUser actor,
        string? fileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (actor.UserId <= 0)
        {
            return (false, "You must be signed in to upload a resume.", null);
        }

        var extracted = extractor.Extract(fileName, content);
        if (!extracted.Success)
        {
            return (false, extracted.Error ?? "The file could not be read.", null);
        }

        var findings = ResumePrivacyChecker.Check(extracted.Text);
        var redacted = redactor.Redact(extracted.Text, MaxRedactedChars);
        var skills = SkillCatalog.Extract(extracted.Text);
        var skillsCsv = ResumeMatcher.ToSkillsCsv(skills);
        var findingsJson = JsonSerializer.Serialize(findings);
        var uploadedAt = FieldRules.Today();
        var safeName = Path.GetFileName(fileName ?? "resume.docx");
        if (safeName.Length is 0 or > 200)
        {
            safeName = "resume.docx";
        }

        await resumes.UpsertAsync(
            actor.UserId,
            safeName,
            redacted,
            skillsCsv,
            findings.Count,
            findingsJson,
            uploadedAt,
            cancellationToken);

        var page = new ResumePageModel(
            new ResumeSummary(safeName, findings.Count, uploadedAt, skills),
            findings,
            skills,
            null,
            null);
        return (true, null, page);
    }

    public async Task<bool> DeleteAsync(ActingUser actor, CancellationToken cancellationToken)
    {
        if (actor.UserId <= 0)
        {
            return false;
        }

        return await resumes.DeleteByUserIdAsync(actor.UserId, cancellationToken);
    }

    public async Task<ResumeMatchResult?> MatchForUserAsync(
        ActingUser actor,
        string? jobText,
        CancellationToken cancellationToken)
    {
        var resume = await resumes.GetByUserIdAsync(actor.UserId, cancellationToken);
        if (resume is null)
        {
            return null;
        }

        return ResumeMatcher.Match(jobText, ResumeMatcher.ParseSkillsCsv(resume.SkillsCsv));
    }

    public Task<ResumeSummary?> GetSummaryAsync(ActingUser actor, CancellationToken cancellationToken) =>
        GetSummaryCoreAsync(actor.UserId, cancellationToken);

    public async Task<ResumeSummary?> GetSummaryForUserAsync(int userId, CancellationToken cancellationToken) =>
        await GetSummaryCoreAsync(userId, cancellationToken);

    private async Task<ResumeSummary?> GetSummaryCoreAsync(int userId, CancellationToken cancellationToken)
    {
        var resume = await resumes.GetByUserIdAsync(userId, cancellationToken);
        return resume is null
            ? null
            : new ResumeSummary(
                resume.FileName,
                resume.PrivacyIssueCount,
                resume.UploadedAt,
                ResumeMatcher.ParseSkillsCsv(resume.SkillsCsv));
    }

    private static IReadOnlyList<PrivacyFinding> DeserializeFindings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<PrivacyFinding>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
