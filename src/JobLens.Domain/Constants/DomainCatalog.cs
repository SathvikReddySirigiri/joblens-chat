namespace JobLens.Domain.Constants;

public static class Paging
{
    public const int PageSize = 15;
}

public static class ScamTypes
{
    public static readonly string[] All =
    [
        "Fake check", "Equipment purchase", "Upfront fee", "Chat-only interview", "Identity theft", "Fake staffing agency",
    ];
}

public static class Channels
{
    public static readonly string[] All =
    [
        "Email", "Telegram", "WhatsApp", "LinkedIn", "Text message",
    ];
}

public static class ReportStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Pending, Approved, Rejected];
}

public static class Verdicts
{
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";

    public static readonly string[] All = [Low, Medium, High];
}
