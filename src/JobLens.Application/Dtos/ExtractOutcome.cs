namespace JobLens.Application.Dtos;

public sealed record ExtractOutcome(bool Success, string Text, string? Error);
