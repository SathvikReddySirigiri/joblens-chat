namespace JobLens.Web.ViewModels;

/// <summary>Dropdown and pager options for MVC forms (mirrors Domain constants where applicable).</summary>
public static class FormOptionsCatalog
{
    public const int PageSize = Domain.Constants.Paging.PageSize;

    public static readonly string[] Industries =
    [
        "Technology", "Consulting", "Finance", "Retail", "Healthcare", "Education",
    ];

    public static readonly string[] ScamTypes = Domain.Constants.ScamTypes.All;

    public static readonly string[] Channels = Domain.Constants.Channels.All;
}
