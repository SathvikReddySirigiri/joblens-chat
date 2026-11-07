using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class OfferAnalyzerController : Controller
{
    [HttpGet("/OfferAnalyzer")]
    public IActionResult Index() => View();
}
