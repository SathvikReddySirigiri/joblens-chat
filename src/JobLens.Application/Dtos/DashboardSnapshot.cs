namespace JobLens.Application.Dtos;

public sealed record DashboardKpis(
    int TotalReports,
    int PendingReports,
    int ApprovedReports,
    double ApprovalRate,
    double TotalAmountRequestedUsd,
    int ChecksThisMonth,
    double HighRiskCheckPct,
    int Companies,
    int JobPostings,
    double? AvgInterviewRating);

public sealed record MonthSeries(string Month, int Total, int Approved);

public sealed record CheckMonth(string Month, int Low, int Medium, int High);

public sealed record CompanyCompare(string CompanyName, int Postings, int ApprovedReports, double? AverageRating);

public sealed record DashboardSnapshot(
    DashboardKpis Kpis,
    IReadOnlyList<MonthSeries> ReportsPerMonth,
    IReadOnlyList<CategoryCount> ReportsByScamType,
    IReadOnlyList<CategoryCount> ReportsByStatus,
    IReadOnlyList<CompanyRanking> TopImpersonatedCompanies,
    IReadOnlyList<CheckMonth> ChecksByVerdictPerMonth,
    IReadOnlyList<CategoryCount> ReportsByChannel,
    IReadOnlyList<CompanyCompare> PostingsVsReports,
    IReadOnlyList<string> Insights,
    string? LatestReportedDate,
    string Range);
