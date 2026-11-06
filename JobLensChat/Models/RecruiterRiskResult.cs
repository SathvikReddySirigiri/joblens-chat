namespace JobLensChat.Models;

public sealed record RecruiterRiskResult(
    string Email,
    string Domain,
    int Score,
    string Verdict,
    string? MatchedCompany,
    string? OfficialDomain,
    int ApprovedReportCount,
    IReadOnlyList<string> Reasons);
