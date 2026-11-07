using JobLens.Application.Dtos;

namespace JobLens.Application.Interfaces;

public interface IOfferTextExtractor
{
    const int MaxBytes = 2 * 1024 * 1024;

    ExtractOutcome Extract(string? fileName, ReadOnlyMemory<byte> content);
}
