using JobLensChat.Data;
using JobLensChat.Services;
using JobLensChat.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class JobPostingsController(
    JobPostingService postings,
    JobPostingRepository repository,
    CompanyRepository companies) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? company,
        bool? remote,
        bool? paid,
        string? state,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewBag.Page = await repository.BrowseAsync(search, company, remote, paid, state, page, cancellationToken);
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
        ViewBag.Search = search;
        ViewBag.Company = company;
        ViewBag.Remote = remote;
        ViewBag.Paid = paid;
        ViewBag.State = state;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var posting = await repository.GetByIdAsync(id, cancellationToken);
        if (posting is null)
        {
            return NotFound();
        }

        var company = await companies.GetByIdAsync(posting.CompanyId, cancellationToken);
        ViewBag.Company = company;
        ViewBag.Activity = await companies.GetActivityAsync(posting.CompanyId, cancellationToken);
        return View(posting);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        return View(new JobPostingFormVm { PostedDate = FieldRules.Today(), IsPaid = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JobPostingFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await postings.CreateAsync(form, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The posting could not be saved.");
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        TempData["Success"] = "Job posting saved.";
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var posting = await repository.GetByIdAsync(id, cancellationToken);
        if (posting is null)
        {
            return NotFound();
        }

        await LoadCompaniesAsync(cancellationToken);
        return View(new JobPostingFormVm
        {
            PostingId = posting.PostingId,
            CompanyId = posting.CompanyId,
            Title = posting.Title,
            Location = posting.Location,
            State = posting.State,
            IsRemote = posting.IsRemote,
            IsPaid = posting.IsPaid,
            PayRatePerHour = posting.PayRatePerHour,
            Description = posting.Description,
            PostedDate = posting.PostedDate,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, JobPostingFormVm form, CancellationToken cancellationToken)
    {
        form.PostingId = id;
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await postings.UpdateAsync(form, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The posting could not be saved.");
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        TempData["Success"] = "Job posting updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var posting = await repository.GetByIdAsync(id, cancellationToken);
        return posting is null ? NotFound() : View(posting);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await postings.DeleteAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Job posting deleted." : result.ErrorMessage;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Delete), new { id });
    }

    private async Task LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
    }
}
