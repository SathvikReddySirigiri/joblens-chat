namespace JobLens.Web.ViewModels;

public sealed record ChatTurn(string Role, string Text, IReadOnlyList<string> ToolsUsed, DateTimeOffset TimeUtc);
