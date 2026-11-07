namespace JobLensChat.Models;

public sealed record JobListing(
    int PostingId,
    string CompanyName,
    string Title,
    string Location,
    string State,
    bool IsRemote,
    bool IsPaid,
    double? PayRatePerHour,
    bool Impersonated);
