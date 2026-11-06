namespace JobLensChat.Models;

public sealed record ScamSummary(
    int TotalReports,
    int ApprovedReports,
    int PendingReports,
    int RejectedReports,
    double TotalAmountRequestedUsd,
    double AverageAmountRequestedUsd,
    string? FromDate,
    string? ToDate,
    IReadOnlyList<CategoryCount> ByScamType,
    IReadOnlyList<CategoryCount> ByChannel);
