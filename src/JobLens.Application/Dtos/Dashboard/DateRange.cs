namespace JobLens.Application.Dtos.Dashboard;

public readonly record struct DateRange(string? FromDate, string? ToDate)
{
    public static DateRange All { get; } = new(null, null);
}
