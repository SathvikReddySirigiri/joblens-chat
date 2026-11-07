using JobLens.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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

        await middleware.InvokeAsync(context, Config("secret-key"), Env(Environments.Development));

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

        await middleware.InvokeAsync(context, Config("secret-key"), Env(Environments.Development));

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

        await middleware.InvokeAsync(context, Config("secret-key"), Env(Environments.Development));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Development_allows_request_when_key_not_configured()
    {
        var context = McpContext();
        var nextCalled = false;
        var middleware = new McpAccessKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, Config(""), Env(Environments.Development));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task NonDevelopment_returns_503_when_key_not_configured()
    {
        var context = McpContext();
        context.Response.Body = new MemoryStream();
        var nextCalled = false;
        var middleware = new McpAccessKeyMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, Config(""), Env(Environments.Production));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.False(nextCalled);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal(McpAccessKeyMiddleware.UnavailableMessage, await reader.ReadToEndAsync());
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

    private static StubEnvironment Env(string name) => new() { EnvironmentName = name };

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "JobLens.Tests";
        public string WebRootPath { get; set; } = "";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
