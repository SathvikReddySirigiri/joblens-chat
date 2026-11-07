namespace JobLensChat.Models;

public sealed record Recruiter(
    int RecruiterId,
    string Email,
    string Domain,
    int? CompanyId,
    string? CompanyName,
    string FirstSeen,
    string Notes,
    int ReportCount);
