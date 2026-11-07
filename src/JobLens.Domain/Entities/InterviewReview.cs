namespace JobLens.Domain.Entities;

public sealed record InterviewReview(
    int ReviewId,
    int CompanyId,
    string CompanyName,
    string Role,
    int Rating,
    string Experience,
    bool WasLegit,
    string ReviewDate,
    int? CreatedByUserId = null);
