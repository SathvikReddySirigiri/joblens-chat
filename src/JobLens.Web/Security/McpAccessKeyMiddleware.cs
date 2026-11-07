using System.Security.Cryptography;
using System.Text;

namespace JobLens.Web.Security;

/// <summary>
/// Protects /mcp when Mcp:AccessKey is set. Missing/wrong X-Api-Key returns 401.
/// </summary>
public sealed class McpAccessKeyMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Api-Key";
    public const string ConfigKey = "Mcp:AccessKey";

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp"))
        {
            await next(context);
            return;
        }

        var expected = configuration[ConfigKey];
        if (string.IsNullOrEmpty(expected))
        {
            await next(context);
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
