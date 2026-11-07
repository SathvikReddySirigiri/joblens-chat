using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class DashboardController : Controller
{
    [HttpGet("/Dashboard")]
    public IActionResult Index() => View();
}
