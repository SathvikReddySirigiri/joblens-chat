namespace JobLensChat.Models;

public sealed record ChatTurn(string Role, string Text, IReadOnlyList<string> ToolsUsed, DateTimeOffset TimeUtc);
