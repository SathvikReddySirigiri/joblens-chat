using JobLens.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class LiveJobsController : Controller
{
    [HttpGet("/LiveJobs")]
    public IActionResult Index(
        string? category,
        string? categoryCode,
        string? level,
        string? location,
        string? companyName,
        string? keyword,
        string? hiringPath,
        bool remoteOnly = false,
        int page = 1)
    {
        return RedirectToAction(
            "Index",
            "Jobs",
            new
            {
                tab = JobsPageVm.LiveTab,
                category,
                categoryCode,
                level,
                location,
                companyName,
                keyword,
                hiringPath,
                remoteOnly,
                page,
            });
    }
}
