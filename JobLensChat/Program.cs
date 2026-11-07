using JobLensChat.Data;
using JobLensChat.Services;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".JobLens.Session";
});
builder.Services.AddSingleton<ScamRepository>();
builder.Services.AddSingleton<CompanyRepository>();
builder.Services.AddSingleton<ScamReportRepository>();
builder.Services.AddSingleton<JobPostingRepository>();
builder.Services.AddSingleton<InterviewReviewRepository>();
builder.Services.AddSingleton<CheckLogRepository>();
builder.Services.AddSingleton<DashboardRepository>();
builder.Services.AddSingleton<RecruiterRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<ScamReportService>();
builder.Services.AddScoped<JobPostingService>();
builder.Services.AddScoped<InterviewReviewService>();
builder.Services.AddScoped<RecruiterService>();
builder.Services.AddSingleton<RecruiterRiskService>();
builder.Services.AddScoped<ChatOrchestrator>();
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
app.UseSession();
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
