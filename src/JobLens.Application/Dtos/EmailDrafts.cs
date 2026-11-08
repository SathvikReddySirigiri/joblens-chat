namespace JobLens.Application.Dtos;

public sealed record EmailDraftRequest(
    string Type,
    int? ApplicationId,
    string? RecruiterName,
    string? RecruiterEmail,
    string CompanyName,
    string RoleTitle,
    string StudentName,
    string Tone,
    string? ExtraNotes);

public sealed record EmailDraftResult(
    string Subject,
    string Body,
    IReadOnlyList<string> Warnings,
    string EffectiveType,
    string Mode);

public sealed record EmailDraftServiceResult(bool Success, bool NotFound, string? ErrorMessage, EmailDraftResult? Draft)
{
    public static EmailDraftServiceResult Ok(EmailDraftResult draft) => new(true, false, null, draft);

    public static EmailDraftServiceResult Fail(string error) => new(false, false, error, null);

    public static EmailDraftServiceResult Missing() => new(false, true, "Application not found.", null);
}
