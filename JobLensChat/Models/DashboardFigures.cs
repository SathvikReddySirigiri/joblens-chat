namespace JobLensChat.Models;

public sealed record MonthCount(string Month, int Count);

public sealed record VerdictMonthCount(string Month, string Verdict, int Count);

public sealed record StateCount(string State, int Count);

public sealed record CompanyRating(string CompanyName, double AverageRating, int ReviewCount);
