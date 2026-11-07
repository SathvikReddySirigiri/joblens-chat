using JobLensChat.Models;

namespace JobLensChat.ViewModels;

public sealed class OfferAnalyzerPageVm
{
    public string? Text { get; set; }

    public string? UploadError { get; set; }

    public OfferAnalysis? Result { get; set; }
}
