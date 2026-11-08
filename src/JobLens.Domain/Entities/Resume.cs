namespace JobLens.Domain.Entities;

public sealed record Resume(
    int ResumeId,
    int UserId,
    string FileName,
    string RedactedText,
    string SkillsCsv,
    int PrivacyIssueCount,
    string PrivacyFindingsJson,
    string UploadedAt);
