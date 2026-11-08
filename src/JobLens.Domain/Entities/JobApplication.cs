namespace JobLens.Domain.Entities;

public sealed record JobApplication(
    int ApplicationId,
    int UserId,
    string CompanyName,
    int? CompanyId,
    string RoleTitle,
    string Source,
    string? JobUrl,
    string? RecruiterEmail,
    string Status,
    string? AppliedDate,
    string? NextFollowUpDate,
    string? Notes,
    string? RiskVerdict,
    string? ClosedDate,
    string CreatedAt,
    string UpdatedAt);
