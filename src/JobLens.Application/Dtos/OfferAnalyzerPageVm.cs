using JobLens.Domain.Entities;
using JobLens.Application.Dtos;

namespace JobLens.Application.Dtos;

public sealed class OfferAnalyzerPageVm
{
    public string? Text { get; set; }

    public string? UploadError { get; set; }

    public OfferAnalysis? Result { get; set; }
}
