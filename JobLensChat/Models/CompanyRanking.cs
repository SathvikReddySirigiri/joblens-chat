namespace JobLensChat.Models;

public sealed record CompanyRanking(
    int Rank,
    string CompanyName,
    string OfficialDomain,
    string Industry,
    int ApprovedReports,
    double TotalAmountRequestedUsd);
