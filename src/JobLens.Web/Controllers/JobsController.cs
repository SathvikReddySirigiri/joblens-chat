using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Web.Security;
using JobLens.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobLens.Web.Controllers;

public sealed class JobsController(
    LiveJobsService liveJobs,
    JobPostingService postings,
    CompanyService companies,
    SmartSearchService smartSearch) : Controller
{
    [HttpGet("/Jobs")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimiting.ExpensivePolicyName)]
    public async Task<IActionResult> Index(
        string? tab,
        string? category,
        string? categoryCode,
        string? level,
        string? location,
        string? companyName,
        string? keyword,
        string? hiringPath,
        bool remoteOnly = false,
        int page = 1,
        string? search = null,
        int? company = null,
        bool? remote = null,
        bool? paid = null,
        string? state = null,
        double? minPay = null,
        bool exclude = false,
        string? q = null,
        bool asked = false,
        CancellationToken cancellationToken = default)
    {
        var resolved = JobsPageVm.ResolveTab(tab);
        if (resolved == JobsPageVm.LiveTab)
        {
            var live = await liveJobs.SearchAsync(
                new LiveJobsQuery(
                    keyword,
                    location,
                    category,
                    categoryCode,
                    level,
                    hiringPath,
                    remoteOnly,
                    companyName,
                    page),
                cancellationToken);
            return View(new JobsPageVm { Tab = JobsPageVm.LiveTab, Live = live });
        }

        await LoadJobLensTabAsync(
            search,
            company,
            remote,
            paid,
            state,
            minPay,
            exclude,
            q,
            asked,
            page,
            cancellationToken);
        return View(new JobsPageVm { Tab = JobsPageVm.JobLensTab });
    }

    [HttpPost("/Jobs/Ask")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(string? q, CancellationToken cancellationToken)
    {
        var question = q?.Trim() ?? "";
        if (question.Length is 0 or > 200)
        {
            TempData["Error"] = "Enter a plain-English search of 200 characters or fewer.";
            return RedirectToAction(nameof(Index), new { tab = JobsPageVm.JobLensTab });
        }

        var filter = await smartSearch.InterpretAsync(question, cancellationToken);
        return RedirectToAction(nameof(Index), new
        {
            tab = JobsPageVm.JobLensTab,
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

    private async Task LoadJobLensTabAsync(
        string? search,
        int? company,
        bool? remote,
        bool? paid,
        string? state,
        double? minPay,
        bool exclude,
        string? q,
        bool asked,
        int page,
        CancellationToken cancellationToken)
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
    }
}
