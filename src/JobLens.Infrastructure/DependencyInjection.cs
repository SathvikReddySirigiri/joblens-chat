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

    public const string UsaJobsProvider = "UsaJobs";
    public const string TheMuseProvider = "TheMuse";

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

        RegisterLiveJobsProvider(services, configuration);

        services.AddSingleton<IScamAnalyticsRepository, ScamAnalyticsRepository>();
        services.AddSingleton<ICompanyRepository, CompanyRepository>();
        services.AddSingleton<IScamReportRepository, ScamReportRepository>();
        services.AddSingleton<IJobPostingRepository, JobPostingRepository>();
        services.AddSingleton<IInterviewReviewRepository, InterviewReviewRepository>();
        services.AddSingleton<ICheckLogRepository, CheckLogRepository>();
        services.AddSingleton<IDashboardRepository, DashboardRepository>();
        services.AddSingleton<IRecruiterRepository, RecruiterRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IJobApplicationRepository, JobApplicationRepository>();
        services.AddSingleton<IResumeRepository, ResumeRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IAiClientFactory, AiClientFactory>();
        services.AddSingleton<IOfferTextExtractor, OfferTextExtractor>();
        return services;
    }

    /// <summary>Jobs:Provider = UsaJobs (default) | TheMuse.</summary>
    public static Type ResolveLiveJobsProviderType(IConfiguration configuration)
    {
        var provider = configuration["Jobs:Provider"]?.Trim() ?? UsaJobsProvider;
        return string.Equals(provider, TheMuseProvider, StringComparison.OrdinalIgnoreCase)
            ? typeof(MuseJobsClient)
            : typeof(UsaJobsClient);
    }

    private static void RegisterLiveJobsProvider(IServiceCollection services, IConfiguration configuration)
    {
        if (ResolveLiveJobsProviderType(configuration) == typeof(MuseJobsClient))
        {
            services.AddHttpClient<ILiveJobsProvider, MuseJobsClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.themuse.com/api/public/");
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (compatible; JobLensStudentProject/1.0; +https://github.com/SathvikReddySirigiri/joblens-chat)");
            });
            return;
        }

        services.AddHttpClient<ILiveJobsProvider, UsaJobsClient>(client =>
        {
            client.BaseAddress = new Uri("https://data.usajobs.gov/api/");
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
    }
}
