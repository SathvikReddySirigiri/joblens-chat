using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class CheckRecruiterController : Controller
{
    [HttpGet("/CheckRecruiter")]
    public IActionResult Index(string? email)
    {
        ViewBag.Email = email;
        return View();
    }
}
