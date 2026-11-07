using JobLens.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class DashboardController(DashboardService dashboards) : Controller
{
    [HttpGet("/Dashboard")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var data = await dashboards.GetDashboardAsync("all", cancellationToken);
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
