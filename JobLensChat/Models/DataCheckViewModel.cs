namespace JobLensChat.Models;

public sealed record DataCheckViewModel(
    string DatabasePath,
    DataCounts Counts,
    IReadOnlyList<CompanyRanking> TopCompanies,
    ScamSummary Summary,
    IReadOnlyList<ScamReport> AmazonLookalikeReports,
    IReadOnlyList<RecruiterRiskResult> RiskChecks);
