namespace JobLens.Application.Dtos;

public sealed record RecruiterRiskResult(
    string Email,
    string Domain,
    int Score,
    string Verdict,
    string? MatchedCompany,
    string? OfficialDomain,
    int ApprovedReportCount,
    IReadOnlyList<RiskReason> Reasons,
    RdapResult? Rdap = null,
    EdgarResult? Edgar = null);
