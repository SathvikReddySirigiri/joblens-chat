using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class JobPostingsController(
    JobPostingService postings,
    CompanyService companies,
    SmartSearchService smartSearch) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        string? search,
        int? company,
        bool? remote,
        bool? paid,
        string? state,
        double? minPay,
        bool exclude = false,
        string? q = null,
        bool asked = false,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var companyList = await companies.GetAllAsync(cancellationToken);
        var selected = companyList.FirstOrDefault(item => item.CompanyId == company);
        var filter = new SearchFilter(search, company, selected?.Name, state, remote, paid, minPay, exclude);
        ViewBag.Page = await postings.SearchPageAsync(filter, page, cancellationToken);
        ViewBag.Companies = companyList;
        ViewBag.Search = search;
        ViewBag.Company = company;
        ViewBag.Remote = remote;
        ViewBag.Paid = paid;
        ViewBag.State = state;
        ViewBag.MinPay = minPay;
        ViewBag.Exclude = exclude;
        ViewBag.Question = q;
        ViewBag.Asked = asked;
        ViewBag.Filter = filter;
        ViewBag.IsModerator = User.IsInRole(ActingUser.ModeratorRole);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(string? q, CancellationToken cancellationToken)
    {
        var question = q?.Trim() ?? "";
        if (question.Length is 0 or > 200)
        {
            TempData["Error"] = "Enter a plain-English search of 200 characters or fewer.";
            return RedirectToAction(nameof(Index));
        }

        var filter = await smartSearch.InterpretAsync(question, cancellationToken);
        return RedirectToAction(nameof(Index), new
        {
            search = filter.Keyword,
            company = filter.CompanyId,
            remote = filter.IsRemote,
            paid = filter.IsPaid,
            state = filter.State,
            minPay = filter.MinPayPerHour,
            exclude = filter.ExcludeCompaniesWithApprovedReports,
            q = question,
            asked = true,
        });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var posting = await postings.GetByIdAsync(id, cancellationToken);
        if (posting is null)
        {
            return NotFound();
        }

        var company = await companies.GetByIdAsync(posting.CompanyId, cancellationToken);
        ViewBag.Company = company;
        ViewBag.Activity = await companies.GetActivityAsync(posting.CompanyId, cancellationToken);
        ViewBag.IsModerator = User.IsInRole(ActingUser.ModeratorRole);
        return View(posting);
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.ModeratorOnly)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        return View(new JobPostingFormVm { PostedDate = FieldRules.Today(), IsPaid = true });
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.ModeratorOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JobPostingFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await postings.CreateAsync(form, ActingUserFactory.Require(User), cancellationToken);
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
    [Authorize(Policy = AuthPolicies.ModeratorOnly)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var posting = await postings.GetByIdAsync(id, cancellationToken);
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
    [Authorize(Policy = AuthPolicies.ModeratorOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, JobPostingFormVm form, CancellationToken cancellationToken)
    {
        form.PostingId = id;
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await postings.UpdateAsync(form, ActingUserFactory.Require(User), cancellationToken);
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
    [Authorize(Policy = AuthPolicies.ModeratorOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var posting = await postings.GetByIdAsync(id, cancellationToken);
        return posting is null ? NotFound() : View(posting);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.ModeratorOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await postings.DeleteAsync(id, ActingUserFactory.Require(User), cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Job posting deleted." : result.ErrorMessage;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Delete), new { id });
    }

    private async Task LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
    }
}
