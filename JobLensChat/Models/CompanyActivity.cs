namespace JobLensChat.Models;

public sealed record CompanyActivity(
    int ApprovedReports,
    int PendingReports,
    int Postings,
    int Reviews,
    double? AverageRating);
