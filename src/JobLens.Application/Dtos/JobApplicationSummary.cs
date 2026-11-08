namespace JobLens.Application.Dtos;

public sealed record JobApplicationStatusCount(string Status, int Count);

public sealed record JobApplicationPipeline(
    int Applied,
    int Interviewing,
    int Offers,
    double ResponseRatePercent,
    IReadOnlyList<JobApplicationStatusCount> ByStatus);

public sealed record JobApplicationListItem(
    int ApplicationId,
    string CompanyName,
    string RoleTitle,
    string Source,
    string Status,
    string? AppliedDate,
    string? NextFollowUpDate,
    string? RecruiterEmail,
    string? RiskVerdict,
    bool FollowUpDue);
