using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class DashboardController(DashboardService dashboards, ApplicationService applications) : Controller
{
    [HttpGet("/Dashboard")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var data = await dashboards.GetDashboardAsync("all", cancellationToken);
        var actor = ActingUserFactory.TryCreate(User);
        if (actor?.IsStudent == true)
        {
            ViewBag.StudentPipeline = await applications.GetPipelineAsync(actor, cancellationToken);
        }
        else if (actor?.IsModerator == true)
        {
            ViewBag.TotalApplications = await applications.CountAllAsync(cancellationToken);
        }

        return View(data);
    }

    [HttpGet("/Dashboard/Data")]
    public async Task<IActionResult> Data(string? range, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var label = string.IsNullOrWhiteSpace(range) ? "all" : range.Trim().ToLowerInvariant();
        if (label is not ("all" or "3m" or "6m"))
        {
            label = "all";
        }

        var data = await dashboards.GetDashboardAsync(label, cancellationToken);
        return Json(data);
    }
}
