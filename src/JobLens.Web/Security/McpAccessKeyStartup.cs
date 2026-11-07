namespace JobLens.Web.Security;

public static class McpAccessKeyStartup
{
    public static void WarnIfOpen(WebApplication app)
    {
        if (!string.IsNullOrEmpty(app.Configuration[McpAccessKeyMiddleware.ConfigKey]))
        {
            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        app.Logger.LogWarning(
            "{ConfigKey} is not configured; the /mcp endpoint is open without an API key (development only).",
            McpAccessKeyMiddleware.ConfigKey);
    }
}
