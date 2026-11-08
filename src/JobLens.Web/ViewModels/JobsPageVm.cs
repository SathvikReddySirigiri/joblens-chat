using JobLens.Application.Dtos;

namespace JobLens.Web.ViewModels;

public sealed class JobsPageVm
{
    public const string LiveTab = "live";
    public const string JobLensTab = "joblens";

    public string Tab { get; init; } = LiveTab;

    public LiveJobsPage? Live { get; init; }

    public bool IsLiveTab => string.Equals(Tab, LiveTab, StringComparison.OrdinalIgnoreCase);

    public static string ResolveTab(string? tab) =>
        string.Equals(tab, JobLensTab, StringComparison.OrdinalIgnoreCase) ? JobLensTab : LiveTab;
}
