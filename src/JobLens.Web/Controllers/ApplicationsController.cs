using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Forms;
using JobLens.Application.Services;
using JobLens.Domain.Constants;
using JobLens.Domain.Entities;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

[Authorize(Policy = AuthPolicies.SignedIn)]
public sealed class ApplicationsController(ApplicationService applications) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? status,
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var actor = ActingUserFactory.Require(User);
        ViewBag.Page = await applications.BrowseAsync(actor, status, search, page, cancellationToken);
        ViewBag.StatusTabs = await applications.GetStatusTabsAsync(actor, cancellationToken);
        ViewBag.Pipeline = await applications.GetPipelineAsync(actor, cancellationToken);
        ViewBag.Status = status;
        ViewBag.Search = search;
        ViewBag.Statuses = ApplicationStatuses.All;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var app = await applications.GetByIdAsync(id, ActingUserFactory.Require(User), cancellationToken);
        if (app is null)
        {
            return NotFound();
        }

        ViewBag.FollowUpDue = ApplicationService.IsFollowUpDue(app);
        return View(app);
    }

    [HttpGet]
    public IActionResult Create(
        string? company,
        string? title,
        string? url,
        string? source)
    {
        var form = new JobApplicationFormVm
        {
            CompanyName = company?.Trim() ?? "",
            RoleTitle = title?.Trim() ?? "",
            JobUrl = url?.Trim(),
            Source = ResolveSource(source),
            Status = ApplicationStatuses.Saved,
            AppliedDate = FieldRules.Today(),
        };
        LoadLookups();
        return View(form);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JobApplicationFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            LoadLookups();
            return View(form);
        }

        var result = await applications.CreateAsync(form, ActingUserFactory.Require(User), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The application could not be saved.");
            LoadLookups();
            return View(form);
        }

        TempData["Success"] = "Application saved.";
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var app = await applications.GetByIdAsync(id, ActingUserFactory.Require(User), cancellationToken);
        if (app is null)
        {
            return NotFound();
        }

        LoadLookups();
        return View(ToForm(app));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(JobApplicationFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            LoadLookups();
            return View(form);
        }

        var result = await applications.UpdateAsync(form, ActingUserFactory.Require(User), cancellationToken);
        if (!result.Success)
        {
            if (string.Equals(result.ErrorMessage, "Application not found.", StringComparison.Ordinal))
            {
                return NotFound();
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The application could not be saved.");
            LoadLookups();
            return View(form);
        }

        TempData["Success"] = "Application updated.";
        return RedirectToAction(nameof(Details), new { id = form.ApplicationId });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var app = await applications.GetByIdAsync(id, ActingUserFactory.Require(User), cancellationToken);
        if (app is null)
        {
            return NotFound();
        }

        return View(app);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await applications.DeleteAsync(id, ActingUserFactory.Require(User), cancellationToken);
        if (!result.Success)
        {
            return NotFound();
        }

        TempData["Success"] = "Application deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickStatus(
        int id,
        string status,
        string? filterStatus,
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await applications.QuickStatusAsync(id, status, ActingUserFactory.Require(User), cancellationToken);
        if (!result.Success)
        {
            return NotFound();
        }

        TempData["Success"] = "Status updated.";
        return RedirectToAction(nameof(Index), new { status = filterStatus, search, page });
    }

    private void LoadLookups()
    {
        ViewBag.Sources = ApplicationSources.All;
        ViewBag.Statuses = ApplicationStatuses.All;
    }

    private static string ResolveSource(string? source)
    {
        var value = source?.Trim() ?? "";
        return ApplicationSources.All.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? value
            : ApplicationSources.Other;
    }

    private static JobApplicationFormVm ToForm(JobApplication app) => new()
    {
        ApplicationId = app.ApplicationId,
        CompanyName = app.CompanyName,
        CompanyId = app.CompanyId,
        RoleTitle = app.RoleTitle,
        Source = app.Source,
        JobUrl = app.JobUrl,
        RecruiterEmail = app.RecruiterEmail,
        Status = app.Status,
        AppliedDate = app.AppliedDate,
        NextFollowUpDate = app.NextFollowUpDate,
        Notes = app.Notes,
    };
}
