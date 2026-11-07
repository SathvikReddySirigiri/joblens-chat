namespace JobLens.Application.Dtos.Dashboard;

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
