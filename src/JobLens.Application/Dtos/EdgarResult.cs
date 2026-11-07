namespace JobLens.Application.Dtos;

public sealed record EdgarResult(
    bool Found,
    string? EntityName,
    string? Cik,
    string? Ticker,
    string Message,
    string Source);
