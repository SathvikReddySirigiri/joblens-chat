namespace JobLens.Application.Dtos;

public sealed record JobPostingListItem(
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
    string PostedDate,
    bool HasApprovedScamReports);
