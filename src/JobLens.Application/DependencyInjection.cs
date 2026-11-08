using JobLens.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobLens.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CompanyService>();
        services.AddScoped<ScamReportService>();
        services.AddScoped<JobPostingService>();
        services.AddScoped<InterviewReviewService>();
        services.AddScoped<RecruiterService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<OfferAnalyzerService>();
        services.AddScoped<SmartSearchService>();
        services.AddScoped<RecruiterRiskService>();
        services.AddScoped<HomeService>();
        services.AddScoped<CheckRecruiterFacade>();
        services.AddScoped<AccountService>();
        services.AddScoped<LiveJobsService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<EmailDraftService>();
        services.AddScoped<ResumeService>();
        services.AddSingleton<PiiRedactor>();
        return services;
    }
}
