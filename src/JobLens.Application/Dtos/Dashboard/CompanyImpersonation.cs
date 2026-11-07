namespace JobLens.Application.Dtos.Dashboard;

public sealed record CompanyImpersonation(
    string CompanyName,
    int ApprovedReports,
    double TotalAmountRequestedUsd);
