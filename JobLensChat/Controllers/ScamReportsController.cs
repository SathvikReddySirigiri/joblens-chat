using JobLensChat.Data;
using JobLensChat.Services;
using JobLensChat.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class ScamReportsController(
    ScamReportService reports,
    ScamReportRepository repository,
    RecruiterRepository recruiters,
    CompanyRepository companies) : Controller
{
    [HttpGet]
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
        var descending = !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        ViewBag.Page = await repository.BrowseAsync(search, status, scamType, channel, company, sort, descending, page, cancellationToken);
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
        ViewBag.PendingCount = await repository.CountByStatusAsync("Pending", cancellationToken);
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.ScamType = scamType;
        ViewBag.Channel = channel;
        ViewBag.Company = company;
        ViewBag.Sort = string.IsNullOrWhiteSpace(sort) ? "date" : sort;
        ViewBag.Dir = descending ? "desc" : "asc";
        ViewBag.ReturnUrl = Request.Path + Request.QueryString;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var report = await repository.GetByIdAsync(id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        ViewBag.Recruiter = await recruiters.GetByEmailAsync(report.RecruiterEmail, cancellationToken);
        ViewBag.Related = await repository.OthersOnDomainAsync(report.Domain, report.ReportId, 5, cancellationToken);
        return View(report);
    }

    [HttpGet]
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScamReportFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await reports.CreateAsync(form, cancellationToken);
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
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var report = await repository.GetByIdAsync(id, cancellationToken);
        if (report is null)
        {
            return NotFound();
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ScamReportFormVm form, CancellationToken cancellationToken)
    {
        form.ReportId = id;
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await reports.UpdateAsync(form, cancellationToken);
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
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var report = await repository.GetByIdAsync(id, cancellationToken);
        return report is null ? NotFound() : View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await reports.DeleteAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Report deleted."
            : result.ErrorMessage;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Delete), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await reports.ApproveAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Report approved." : result.ErrorMessage;
        return Back(returnUrl, id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await reports.RejectAsync(id, cancellationToken);
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
