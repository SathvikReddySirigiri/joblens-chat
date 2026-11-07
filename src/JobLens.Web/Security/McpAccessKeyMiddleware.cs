using System.Security.Cryptography;
using System.Text;

namespace JobLens.Web.Security;

/// <summary>
/// Protects /mcp when Mcp:AccessKey is set. Missing/wrong X-Api-Key returns 401.
/// Outside Development, an empty AccessKey fails closed with 503.
/// </summary>
public sealed class McpAccessKeyMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Api-Key";
    public const string ConfigKey = "Mcp:AccessKey";
    public const string UnavailableMessage = "MCP endpoint is not configured.";

    public async Task InvokeAsync(
        HttpContext context,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp"))
        {
            await next(context);
            return;
        }

        var expected = configuration[ConfigKey];
        if (string.IsNullOrEmpty(expected))
        {
            if (environment.IsDevelopment())
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(UnavailableMessage);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();
        if (!KeysEqual(expected, provided))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    internal static bool KeysEqual(string expected, string provided)
    {
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(provided ?? "");
        return left.Length == right.Length
            && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
