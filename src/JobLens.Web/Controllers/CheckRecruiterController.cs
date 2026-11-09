using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Pages;
using JobLens.Application.Services;
using JobLens.Domain.Entities;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobLens.Web.Controllers;

public sealed class CheckRecruiterController(CheckRecruiterFacade checks) : Controller
{
    [HttpGet("/CheckRecruiter")]
    public async Task<IActionResult> Index(string? email, CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        await LoadRecentAsync(cancellationToken);
        return View(new CheckRecruiterPageVm { Email = email ?? "" });
    }

    [HttpPost("/CheckRecruiter")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(LoginRateLimiting.ExpensivePolicyName)]
    public async Task<IActionResult> Check(CheckRecruiterPageVm form, CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        await LoadRecentAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return View("Index", form);
        }

        var companyList = ViewBag.Companies as IReadOnlyList<Company> ?? [];
        var claimed = companyList.FirstOrDefault(company => company.CompanyId == form.CompanyId)?.Name;
        var actor = ActingUserFactory.TryCreate(User);
        var (result, reports, recruiter) = await checks.CheckAsync(
            form.Email,
            claimed,
            actor?.UserId,
            cancellationToken);
        form.Result = result;
        form.Reports = reports;
        form.Recruiter = recruiter;
        await LoadRecentAsync(cancellationToken);
        return View("Index", form);
    }

    private async Task LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Companies = await checks.GetCompaniesAsync(cancellationToken);
    }

    private async Task LoadRecentAsync(CancellationToken cancellationToken)
    {
        var actor = ActingUserFactory.TryCreate(User);
        ViewBag.RecentChecks = actor is null
            ? Array.Empty<RecentCheckItem>()
            : await checks.RecentForUserAsync(actor.UserId, cancellationToken);
        ViewBag.IsSignedIn = actor is not null;
    }
}
