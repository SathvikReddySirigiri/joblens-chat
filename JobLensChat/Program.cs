using JobLensChat.Data;
using JobLensChat.Services;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// TODO: register session in a later phase.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<ScamRepository>();
builder.Services.AddSingleton<RecruiterRiskService>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Tools never initiate sampling or elicitation, so the server keeps no client session.
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithToolsFromAssembly();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapMcp("/mcp");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Schema and seed must exist before the first request reads the database.
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("JobLensChat.Data");
await DatabaseInitializer.InitializeAsync(
    app.Configuration,
    app.Environment,
    startupLogger,
    app.Lifetime.ApplicationStopping);

app.Run();
