namespace JobLens.Web;

public static class Catalog
{
    public const int PageSize = Domain.Constants.Paging.PageSize;

    public static readonly string[] Industries =
    [
        "Technology", "Consulting", "Finance", "Retail", "Healthcare", "Education",
    ];

    public static readonly string[] ScamTypes = Domain.Constants.ScamTypes.All;

    public static readonly string[] Channels = Domain.Constants.Channels.All;
}
