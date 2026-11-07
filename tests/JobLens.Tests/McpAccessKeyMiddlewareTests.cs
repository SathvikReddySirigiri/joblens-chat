using JobLens.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace JobLens.Tests;

public sealed class McpAccessKeyMiddlewareTests
{
    [Fact]
    public async Task Missing_header_returns_401_when_key_configured()
    {
        var context = McpContext();
        var nextCalled = false;
        var middleware = new McpAccessKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, Config("secret-key"));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Wrong_header_returns_401_when_key_configured()
    {
        var context = McpContext();
        context.Request.Headers[McpAccessKeyMiddleware.HeaderName] = "wrong-key";
        var nextCalled = false;
        var middleware = new McpAccessKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, Config("secret-key"));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Matching_header_is_allowed_when_key_configured()
    {
        var context = McpContext();
        context.Request.Headers[McpAccessKeyMiddleware.HeaderName] = "secret-key";
        var nextCalled = false;
        var middleware = new McpAccessKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, Config("secret-key"));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Request_is_allowed_when_key_not_configured()
    {
        var context = McpContext();
        var nextCalled = false;
        var middleware = new McpAccessKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, Config(""));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextCalled);
    }

    [Fact]
    public void KeysEqual_uses_fixed_time_friendly_comparison()
    {
        Assert.True(McpAccessKeyMiddleware.KeysEqual("abc", "abc"));
        Assert.False(McpAccessKeyMiddleware.KeysEqual("abc", "abd"));
        Assert.False(McpAccessKeyMiddleware.KeysEqual("abc", "ab"));
    }

    private static DefaultHttpContext McpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/mcp";
        return context;
    }

    private static IConfiguration Config(string accessKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [McpAccessKeyMiddleware.ConfigKey] = accessKey,
            })
            .Build();
}
