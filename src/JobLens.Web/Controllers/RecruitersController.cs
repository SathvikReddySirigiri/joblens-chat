using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class RecruitersController(RecruiterService recruiters, CompanyService companies) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? company, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewBag.Page = await recruiters.BrowseAsync(search, company, page, cancellationToken);
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
        ViewBag.Search = search;
        ViewBag.Company = company;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var recruiter = await recruiters.GetByIdAsync(id, cancellationToken);
        return recruiter is null ? NotFound() : View(recruiter);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        return View(new RecruiterFormVm { FirstSeen = FieldRules.Today() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RecruiterFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await recruiters.CreateAsync(form, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The recruiter could not be saved.");
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        TempData["Success"] = "Recruiter saved.";
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var recruiter = await recruiters.GetByIdAsync(id, cancellationToken);
        if (recruiter is null)
        {
            return NotFound();
        }

        await LoadCompaniesAsync(cancellationToken);
        return View(new RecruiterFormVm
        {
            RecruiterId = recruiter.RecruiterId,
            Email = recruiter.Email,
            CompanyId = recruiter.CompanyId,
            FirstSeen = recruiter.FirstSeen,
            Notes = recruiter.Notes,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RecruiterFormVm form, CancellationToken cancellationToken)
    {
        form.RecruiterId = id;
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await recruiters.UpdateAsync(form, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The recruiter could not be saved.");
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        TempData["Success"] = "Recruiter updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var recruiter = await recruiters.GetByIdAsync(id, cancellationToken);
        return recruiter is null ? NotFound() : View(recruiter);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await recruiters.DeleteAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Recruiter deleted." : result.ErrorMessage;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Delete), new { id });
    }

    private async Task LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
    }
}
