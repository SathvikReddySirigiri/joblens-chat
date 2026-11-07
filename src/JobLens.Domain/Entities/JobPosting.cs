namespace JobLens.Domain.Entities;

public sealed record JobPosting(
    int PostingId,
    int CompanyId,
    string CompanyName,
    string Title,
    string Location,
    string State,
    bool IsRemote,
    bool IsPaid,
    double? PayRatePerHour,
    string Description,
    string PostedDate);
