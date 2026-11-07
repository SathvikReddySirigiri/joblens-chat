namespace JobLens.Application.Dtos.Dashboard;

/// <summary>Postings vs approved reports for one company on the dashboard compare chart.</summary>
public sealed record CompanyActivity(
    string CompanyName,
    int Postings,
    int ApprovedReports,
    double? AverageRating);
