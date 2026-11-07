using JobLens.Application;
using JobLens.Application.Dtos;
using JobLens.Infrastructure;
using JobLens.Infrastructure.Persistence;
using JobLens.Web.Chat;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Azure App Service terminates TLS and forwards the original scheme/client IP.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
#pragma warning disable ASPDEPR005 // KnownNetworks renamed to KnownIPNetworks in recent ASP.NET Core.
    options.KnownNetworks.Clear();
#pragma warning restore ASPDEPR005
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".JobLens.Session";
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = ".JobLens.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.ModeratorOnly, policy => policy.RequireRole(ActingUser.ModeratorRole));
    options.AddPolicy(AuthPolicies.SignedIn, policy => policy.RequireAuthenticatedUser());
});
builder.Services.AddLoginRateLimiter();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpClient(ChatOrchestrator.McpHttpClientName);
builder.Services.AddScoped<ChatOrchestrator>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Tools never initiate sampling or elicitation, so the server keeps no client session.
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
// Keep /mcp clients on raw status codes (401/503) instead of HTML error pages.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/mcp"),
    branch => branch.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}"));
app.UseMiddleware<McpAccessKeyMiddleware>();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

McpAccessKeyStartup.WarnIfOpen(app);

app.MapMcp("/mcp");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Schema and seed must exist before the first request reads the database.
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("JobLens.Infrastructure.Persistence");
await DatabaseInitializer.InitializeAsync(
    app.Configuration,
    app.Environment,
    startupLogger,
    app.Lifetime.ApplicationStopping);

app.Run();
