namespace JobLens.Web.Models;

public sealed record ChatResult(string Answer, IReadOnlyList<string> ToolsUsed, string Mode);
