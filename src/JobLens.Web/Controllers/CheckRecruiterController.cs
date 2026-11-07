using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class CheckRecruiterController(CheckRecruiterFacade checks) : Controller
{
    [HttpGet("/CheckRecruiter")]
    public async Task<IActionResult> Index(string? email, CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        return View(new CheckRecruiterPageVm { Email = email ?? "" });
    }

    [HttpPost("/CheckRecruiter")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Check(CheckRecruiterPageVm form, CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return View("Index", form);
        }

        var companyList = ViewBag.Companies as IReadOnlyList<Company> ?? [];
        var claimed = companyList.FirstOrDefault(company => company.CompanyId == form.CompanyId)?.Name;
        var (result, reports, recruiter) = await checks.CheckAsync(form.Email, claimed, cancellationToken);
        form.Result = result;
        form.Reports = reports;
        form.Recruiter = recruiter;
        return View("Index", form);
    }

    private async Task LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Companies = await checks.GetCompaniesAsync(cancellationToken);
    }
}
