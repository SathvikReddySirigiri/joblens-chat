using JobLens.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class AboutController(HomeService home) : Controller
{
    [HttpGet("/About")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "About Us";
        return View(await home.GetCountsAsync(cancellationToken));
    }

    [HttpGet("/About/Architecture")]
    public IActionResult Architecture()
    {
        ViewData["Title"] = "Architecture";
        return View();
    }
}
