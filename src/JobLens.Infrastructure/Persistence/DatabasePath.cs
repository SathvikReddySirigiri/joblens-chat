using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

public static class DatabasePath
{
    public static string Resolve(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Database:Path"];
        string path;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            path = Path.GetFullPath(configured);
        }
        else
        {
            var instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrWhiteSpace(instanceId) && !string.IsNullOrWhiteSpace(home))
            {
                // Azure App Service: persist SQLite on HOME/data/JobLens/joblens.db (content share).
                // Directory.CreateDirectory below creates the folder when missing.
                path = Path.Combine(home, "data", "JobLens", "joblens.db");
            }
            else
            {
                path = Path.Combine(environment.ContentRootPath, "App_Data", "joblens.db");
            }
        }

        var fullPath = Path.GetFullPath(path);
        var webRoot = environment.WebRootPath;
        if (!string.IsNullOrEmpty(webRoot)
            && fullPath.StartsWith(Path.GetFullPath(webRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            // A database under wwwroot would be downloadable as a static file.
            throw new InvalidOperationException("The SQLite database must not be stored under wwwroot.");
        }

        var folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        return fullPath;
    }
}
