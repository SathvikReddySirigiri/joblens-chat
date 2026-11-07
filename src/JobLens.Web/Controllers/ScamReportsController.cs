using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class ScamReportsController(
    ScamReportService reports,
    RecruiterService recruiters,
    CompanyService companies) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? scamType,
        string? channel,
        int? company,
        string? sort,
        string? dir,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var viewer = ActingUserFactory.TryCreate(User);
        var descending = !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        ViewBag.Page = await reports.BrowseAsync(search, status, scamType, channel, company, sort, descending, page, viewer, cancellationToken);
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
        ViewBag.PendingCount = viewer?.IsModerator == true
            ? await reports.CountByStatusAsync("Pending", cancellationToken)
            : 0;
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.ScamType = scamType;
        ViewBag.Channel = channel;
        ViewBag.Company = company;
        ViewBag.Sort = string.IsNullOrWhiteSpace(sort) ? "date" : sort;
        ViewBag.Dir = descending ? "desc" : "asc";
        ViewBag.ReturnUrl = Request.Path + Request.QueryString;
        ViewBag.IsModerator = reports.CanModerate(viewer);
        ViewBag.IsSignedIn = viewer is not null;
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        var viewer = ActingUserFactory.TryCreate(User);
        if (!reports.CanView(report, viewer))
        {
            return NotFound();
        }

        ViewBag.Recruiter = await recruiters.GetByEmailAsync(report.RecruiterEmail, cancellationToken);
        ViewBag.Related = await reports.OthersOnDomainAsync(report.Domain, report.ReportId, 5, viewer, cancellationToken);
        ViewBag.CanEdit = reports.CanEdit(report, viewer);
        ViewBag.CanDelete = reports.CanDelete(report, viewer);
        ViewBag.CanModerate = reports.CanModerate(viewer);
        return View(report);
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    public async Task<IActionResult> Create(string? email, int? company, CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        return View(new ScamReportFormVm
        {
            RecruiterEmail = email ?? "",
            CompanyId = company ?? 0,
            ReportedDate = FieldRules.Today(),
        });
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScamReportFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await reports.CreateAsync(form, ActingUserFactory.Require(User), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The report could not be saved.");
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        TempData["Success"] = "Report saved as Pending.";
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        var actor = ActingUserFactory.Require(User);
        if (!reports.CanEdit(report, actor))
        {
            return Forbid();
        }

        await LoadCompaniesAsync(cancellationToken);
        return View(new ScamReportFormVm
        {
            ReportId = report.ReportId,
            CompanyId = report.CompanyId,
            RecruiterEmail = report.RecruiterEmail,
            ScamType = report.ScamType,
            Channel = report.Channel,
            AmountRequestedUsd = report.AmountRequestedUsd,
            DomainAgeDays = report.DomainAgeDays,
            Description = report.Description,
            ReportedDate = report.ReportedDate,
        });
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ScamReportFormVm form, CancellationToken cancellationToken)
    {
        form.ReportId = id;
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await reports.UpdateAsync(form, ActingUserFactory.Require(User), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The report could not be saved.");
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        TempData["Success"] = "Report updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var report = await reports.GetByIdAsync(id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        if (!reports.CanDelete(report, ActingUserFactory.Require(User)))
        {
            return Forbid();
        }

        return View(report);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await reports.DeleteAsync(id, ActingUserFactory.Require(User), cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Report deleted."
            : result.ErrorMessage;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Delete), new { id });
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.ModeratorOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await reports.ApproveAsync(id, ActingUserFactory.Require(User), cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Report approved." : result.ErrorMessage;
        return Back(returnUrl, id);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.ModeratorOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await reports.RejectAsync(id, ActingUserFactory.Require(User), cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Report rejected." : result.ErrorMessage;
        return Back(returnUrl, id);
    }

    private IActionResult Back(string? returnUrl, int id)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
    }
}
