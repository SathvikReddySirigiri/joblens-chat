namespace JobLens.Web.ViewModels;

public sealed record ChatResult(string Answer, IReadOnlyList<string> ToolsUsed, string Mode);
