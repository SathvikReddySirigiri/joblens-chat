namespace JobLens.Domain.Entities;

public sealed record CheckLog(
    int CheckId,
    string CheckType,
    string InputSummary,
    int RiskScore,
    string Verdict,
    string CreatedAt,
    int? UserId = null);
