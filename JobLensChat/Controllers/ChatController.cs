using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class ChatController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
