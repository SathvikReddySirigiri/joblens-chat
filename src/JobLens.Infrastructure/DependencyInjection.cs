using JobLens.Application.Interfaces;
using JobLens.Infrastructure.AI;
using JobLens.Infrastructure.Documents;
using JobLens.Infrastructure.External;
using JobLens.Infrastructure.Persistence;
using JobLens.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobLens.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Shared MemoryCache budget; each RDAP/EDGAR entry must set Size.</summary>
    public const long MemoryCacheSizeLimit = 256;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = MemoryCacheSizeLimit;
        });

        services.AddHttpClient<IRdapClient, RdapClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("JobLens.Web/1.0 (ISM6225 class project)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/rdap+json");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient<IEdgarClient, EdgarClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<IScamQueryRepository, ScamRepository>();
        services.AddSingleton<ICompanyRepository, CompanyRepository>();
        services.AddSingleton<IScamReportRepository, ScamReportRepository>();
        services.AddSingleton<IJobPostingRepository, JobPostingRepository>();
        services.AddSingleton<IInterviewReviewRepository, InterviewReviewRepository>();
        services.AddSingleton<ICheckLogRepository, CheckLogRepository>();
        services.AddSingleton<IDashboardRepository, DashboardRepository>();
        services.AddSingleton<IRecruiterRepository, RecruiterRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IAiClientFactory, AiClientFactory>();
        services.AddSingleton<IOfferTextExtractor, OfferTextExtractor>();
        services.AddSingleton<IDatabaseAdmin, DatabaseAdmin>();
        return services;
    }
}
