using JobLens.Application.Dtos;

namespace JobLens.Application.Dtos.Dashboard;

public sealed record DashboardData(
    DashboardKpis Kpis,
    IReadOnlyList<MonthCount> ReportsPerMonth,
    IReadOnlyList<CategoryCount> ReportsByScamType,
    IReadOnlyList<CategoryCount> ReportsByStatus,
    IReadOnlyList<CompanyImpersonation> TopImpersonatedCompanies,
    IReadOnlyList<VerdictMonth> ChecksByVerdictPerMonth,
    IReadOnlyList<CategoryCount> ReportsByChannel,
    IReadOnlyList<CompanyActivity> CompanyActivity,
    IReadOnlyList<DashboardInsight> Insights,
    string? LatestReportedDate,
    string Range);
