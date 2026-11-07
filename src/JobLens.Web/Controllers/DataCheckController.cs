using JobLens.Application.Services;
using JobLens.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class DataCheckController(
    DataCheckService dataCheck,
    IWebHostEnvironment environment,
    ILogger<DataCheckController> logger) : Controller
{
    [HttpGet("/DataCheck")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var (counts, top, summary, amazon, risks, rdap) = await dataCheck.BuildAsync(cancellationToken);
        return View(new DataCheckViewModel(
            dataCheck.DatabasePath,
            counts,
            top,
            summary,
            amazon,
            risks,
            rdap));
    }

    [HttpPost("/DataCheck/Reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            await dataCheck.ResetAsync(cancellationToken);
            TempData["ResetMessage"] = "Demo data was reset.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo data reset failed.");
            TempData["ResetMessage"] = "Demo data could not be reset. See the server log for details.";
        }

        return RedirectToAction(nameof(Index));
    }
}
