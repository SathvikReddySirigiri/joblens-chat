namespace JobLens.Domain.Entities;

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
    string ReportedDate,
    string Description = "",
    int? CreatedByUserId = null);
