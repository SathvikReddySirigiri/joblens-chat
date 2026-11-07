using System.Diagnostics;
using JobLensChat.Data;
using JobLensChat.Models;
using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class HomeController(ScamRepository repository) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await repository.GetCountsAsync(cancellationToken));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
