using System.Diagnostics;
using System.Text.Json;

namespace JobLens.Web.Mcp;

/// <summary>Uniform MCP tool Information logs: args redacted JSON + elapsed ms. No secrets or PII.</summary>
internal static class McpToolLog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Stopwatch Start() => Stopwatch.StartNew();

    public static void Information(ILogger logger, string toolName, object? redactedArgs, Stopwatch timer)
    {
        timer.Stop();
        var argsJson = JsonSerializer.Serialize(redactedArgs ?? new { }, JsonOptions);
        logger.LogInformation("MCP tool {ToolName} args={Args} ms={ElapsedMs}", toolName, argsJson, timer.ElapsedMilliseconds);
    }

    public static string RedactEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "";
        }

        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1)
        {
            return "[redacted]";
        }

        return "[redacted]@" + email[(at + 1)..].Trim().ToLowerInvariant();
    }
}
