namespace JobLens.Application.Dtos;

public sealed record CompanyActivity(
    int ApprovedReports,
    int PendingReports,
    int Postings,
    int Reviews,
    double? AverageRating);
