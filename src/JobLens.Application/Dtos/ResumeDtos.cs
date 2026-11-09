namespace JobLens.Application.Dtos;

public sealed record PrivacyFinding(
    string Category,
    string Severity,
    string MaskedLine,
    string Advice);

public sealed record ResumeMatchResult(
    int ScorePercent,
    IReadOnlyList<string> RequiredSkills,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills);

public sealed record ResumePageModel(
    ResumeSummary? Resume,
    IReadOnlyList<PrivacyFinding> Findings,
    IReadOnlyList<string> Skills,
    string? UploadError,
    ResumeMatchResult? Match);

public sealed record ResumeSummary(
    string FileName,
    int PrivacyIssueCount,
    string UploadedAt,
    IReadOnlyList<string> Skills);
