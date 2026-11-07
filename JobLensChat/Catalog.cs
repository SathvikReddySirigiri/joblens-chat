namespace JobLensChat;

public static class Catalog
{
    public const int PageSize = 15;

    public static readonly string[] Industries =
    [
        "Technology", "Consulting", "Finance", "Retail", "Healthcare", "Education",
    ];

    public static readonly string[] ScamTypes =
    [
        "Fake check", "Equipment purchase", "Upfront fee", "Chat-only interview", "Identity theft", "Fake staffing agency",
    ];

    public static readonly string[] Channels =
    [
        "Email", "Telegram", "WhatsApp", "LinkedIn", "Text message",
    ];
}
