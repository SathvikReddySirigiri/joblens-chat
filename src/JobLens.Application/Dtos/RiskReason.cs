namespace JobLens.Application.Dtos;

public sealed record RiskReason(string Tone, string Text)
{
    public override string ToString() => Text;
}
