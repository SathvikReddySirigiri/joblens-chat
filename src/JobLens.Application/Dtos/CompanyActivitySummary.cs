namespace JobLens.Application.Dtos;

public sealed record CompanyActivitySummary(
    int ApprovedReports,
    int PendingReports,
    int Postings,
    int Reviews,
    double? AverageRating);

