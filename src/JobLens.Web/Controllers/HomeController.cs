using System.Diagnostics;
using JobLens.Application.Services;
using JobLens.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class HomeController(HomeService home, IWebHostEnvironment environment) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Home";
        return View(await home.GetCountsAsync(cancellationToken));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            ShowDetails = environment.IsDevelopment(),
        });
    }

    [HttpGet("/Home/StatusCode")]
    public IActionResult StatusCodePage(int code)
    {
        Response.StatusCode = code;
        return code switch
        {
            StatusCodes.Status403Forbidden => View("~/Views/Account/AccessDenied.cshtml"),
            StatusCodes.Status404NotFound => View("NotFound"),
            _ => View("StatusCode", code),
        };
    }
}
