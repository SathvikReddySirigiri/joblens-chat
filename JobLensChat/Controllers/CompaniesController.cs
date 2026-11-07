using JobLensChat.Data;
using JobLensChat.Models;
using JobLensChat.Services;
using JobLensChat.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class CompaniesController(CompanyService companies, CompanyRepository repository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var all = await repository.GetAllAsync(cancellationToken);
        var term = search?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(term)
            ? all
            : all.Where(company =>
                company.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || company.OfficialDomain.Contains(term, StringComparison.OrdinalIgnoreCase)
                || company.Industry.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        var size = JobLensChat.Catalog.PageSize;
        var index = Math.Max(page, 1);
        var items = filtered.Skip((index - 1) * size).Take(size).ToList();
        ViewBag.Page = new PageResult<Company>(items, index, size, filtered.Count);
        ViewBag.Search = search;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var company = await repository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return NotFound();
        }

        ViewBag.Activity = await repository.GetActivityAsync(id, cancellationToken);
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
        var company = await repository.GetByIdAsync(id, cancellationToken);
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
        var company = await repository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return NotFound();
        }

        ViewBag.Dependents = await repository.CountDependentsAsync(id, cancellationToken);
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
