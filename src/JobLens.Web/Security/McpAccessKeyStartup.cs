namespace JobLens.Web.Security;

public static class McpAccessKeyStartup
{
    public static void WarnIfOpen(WebApplication app)
    {
        if (!string.IsNullOrEmpty(app.Configuration[McpAccessKeyMiddleware.ConfigKey]))
        {
            return;
        }

        if (app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning(
                "{ConfigKey} is not configured; the /mcp endpoint is open without an API key (development only).",
                McpAccessKeyMiddleware.ConfigKey);
            return;
        }

        app.Logger.LogError(
            "{ConfigKey} is not configured; /mcp will return 503 until an access key is set in App Settings.",
            McpAccessKeyMiddleware.ConfigKey);
    }
}
