namespace JobLensChat.Models;

public sealed record ScamReport(
    int ReportId,
    int CompanyId,
    string CompanyName,
    string RecruiterEmail,
    string Domain,
    string ScamType,
    string Channel,
    double AmountRequestedUsd,
    int DomainAgeDays,
    string Status,
    string ReportedDate);
