using System.ComponentModel.DataAnnotations;

namespace JobLens.Web.ViewModels;

public sealed class ChatViewModel
{
    [Required, StringLength(500, MinimumLength = 2)]
    [Display(Name = "Ask about job-scam data")]
    public string Question { get; set; } = "";

    public IReadOnlyList<ChatTurn> Turns { get; set; } = [];

    public string ProviderLabel { get; set; } = "";

    public bool IsAiMode { get; set; }

    public IReadOnlyList<string> AvailableTools { get; set; } = [];

    public IReadOnlyList<string> SampleQuestions { get; set; } = [];
}
