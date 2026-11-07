namespace JobLens.Web.ViewModels;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowDetails { get; set; }

    public bool ShowRequestId => ShowDetails && !string.IsNullOrEmpty(RequestId);
}
