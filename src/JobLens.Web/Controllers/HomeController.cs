using System.Diagnostics;
using JobLens.Application.Services;
using JobLens.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class HomeController(HomeService home) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await home.GetCountsAsync(cancellationToken));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
