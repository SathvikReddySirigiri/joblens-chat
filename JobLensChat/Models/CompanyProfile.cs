namespace JobLensChat.Models;

public sealed record CompanyProfile(
    string Name,
    string OfficialDomain,
    string Industry,
    int OpenPostings,
    double? AverageRating,
    int ReviewCount,
    int ApprovedReports,
    int PendingReports);
