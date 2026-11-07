using JobLensChat.Data;
using JobLensChat.Models;
using JobLensChat.Services;
using JobLensChat.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class CheckRecruiterController(
    RecruiterRiskService risk,
    CompanyRepository companies,
    ScamReportRepository reports,
    RecruiterRepository recruiters,
    CheckLogRepository logs) : Controller
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
        var result = await risk.CheckAsync(form.Email, claimed, cancellationToken);
        await logs.AddAsync(
            new CheckLog(0, "Recruiter", result.Domain, result.Score, result.Verdict, FieldRules.Today()),
            cancellationToken);

        var related = await reports.BrowseAsync(result.Domain, "Approved", null, null, null, "date", true, 1, cancellationToken);
        form.Result = result;
        form.Reports = related.Items.Take(5).ToList();
        form.Recruiter = await recruiters.GetByEmailAsync(result.Email, cancellationToken);
        return View("Index", form);
    }

    private async Task LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
    }
}
