using JobLens.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class CaseStudiesController : Controller
{
    [HttpGet("/CaseStudies")]
    [AllowAnonymous]
    public IActionResult Index() => View(CaseStudyCatalog.All);

    [HttpGet("/CaseStudies/{id}")]
    [AllowAnonymous]
    public IActionResult Details(string id)
    {
        var study = CaseStudyCatalog.Find(id);
        if (study is null)
        {
            return NotFound();
        }

        var index = CaseStudyCatalog.All.ToList().FindIndex(item => item.Id == study.Id);
        ViewBag.Previous = index > 0 ? CaseStudyCatalog.All[index - 1] : null;
        ViewBag.Next = index >= 0 && index < CaseStudyCatalog.All.Count - 1
            ? CaseStudyCatalog.All[index + 1]
            : null;
        return View(study);
    }
}
