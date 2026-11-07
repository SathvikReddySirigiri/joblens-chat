namespace JobLensChat.Models;

public sealed record OfferFlag(string Flag, string Evidence, string Severity);

public sealed record OfferClause(string Clause, string Explanation);

public sealed record OfferAnalysis(
    int RiskScore,
    string Verdict,
    IReadOnlyList<OfferFlag> RedFlags,
    IReadOnlyList<OfferClause> RiskyClauses,
    string Summary,
    string Mode);
