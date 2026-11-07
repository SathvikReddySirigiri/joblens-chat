using JobLens.Application.Dtos;
using JobLens.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class CompaniesController(CompanyService companies) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewBag.Page = await companies.BrowseAsync(search, page, cancellationToken);
        ViewBag.Search = search;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return NotFound();
        }

        ViewBag.Activity = await companies.GetActivityAsync(id, cancellationToken);
        return View(company);
    }

    [HttpGet]
    public IActionResult Create() => View(new CompanyFormVm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await companies.CreateAsync(form, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The company could not be saved.");
            return View(form);
        }

        TempData["Success"] = "Company saved.";
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return NotFound();
        }

        return View(new CompanyFormVm
        {
            CompanyId = company.CompanyId,
            Name = company.Name,
            OfficialDomain = company.OfficialDomain,
            Industry = company.Industry,
            HqState = company.HqState,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CompanyFormVm form, CancellationToken cancellationToken)
    {
        form.CompanyId = id;
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await companies.UpdateAsync(form, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The company could not be saved.");
            return View(form);
        }

        TempData["Success"] = "Company updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return NotFound();
        }

        ViewBag.Dependents = await companies.CountDependentsAsync(id, cancellationToken);
        return View(company);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await companies.DeleteAsync(id, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Delete), new { id });
        }

        TempData["Success"] = "Company deleted.";
        return RedirectToAction(nameof(Index));
    }
}
