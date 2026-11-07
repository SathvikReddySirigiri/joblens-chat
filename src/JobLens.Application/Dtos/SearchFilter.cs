namespace JobLens.Application.Dtos;

public sealed record SearchFilter(
    string? Keyword,
    int? CompanyId,
    string? CompanyName,
    string? State,
    bool? IsRemote,
    bool? IsPaid,
    double? MinPayPerHour,
    bool ExcludeCompaniesWithApprovedReports);
