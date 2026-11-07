using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Web.ViewModels;

public sealed record DataCheckViewModel(
    string DatabasePath,
    DataCounts Counts,
    IReadOnlyList<CompanyRanking> TopCompanies,
    ScamSummary Summary,
    IReadOnlyList<ScamReport> AmazonLookalikeReports,
    IReadOnlyList<RecruiterRiskResult> RiskChecks,
    IReadOnlyList<RdapResult> RdapLookups);
