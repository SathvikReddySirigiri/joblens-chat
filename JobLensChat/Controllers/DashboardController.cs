using JobLensChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class DashboardController(DashboardService dashboards) : Controller
{
    [HttpGet("/Dashboard")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var snapshot = await dashboards.GetAsync("all", cancellationToken);
        return View(snapshot);
    }

    [HttpGet("/Dashboard/Data")]
    public async Task<IActionResult> Data(string? range, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var snapshot = await dashboards.GetAsync(range, cancellationToken);
        return Json(snapshot);
    }
}
