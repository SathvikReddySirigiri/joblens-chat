using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

public sealed class InterviewReviewsController(InterviewReviewService reviews, CompanyService companies) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(int? company, int? rating, bool? legit, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewBag.Page = await reviews.BrowseAsync(company, rating, legit, page, cancellationToken);
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
        ViewBag.Company = company;
        ViewBag.Rating = rating;
        ViewBag.Legit = legit;
        ViewBag.IsSignedIn = User.Identity?.IsAuthenticated == true;
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdAsync(id, cancellationToken);
        if (review is null)
        {
            return NotFound();
        }

        var actor = ActingUserFactory.TryCreate(User);
        ViewBag.CanEdit = reviews.CanEdit(review, actor);
        ViewBag.CanDelete = reviews.CanDelete(review, actor);
        return View(review);
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        return View(new InterviewReviewFormVm { ReviewDate = FieldRules.Today(), Rating = 5, WasLegit = true });
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InterviewReviewFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await reviews.CreateAsync(form, ActingUserFactory.Require(User), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The review could not be saved.");
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        TempData["Success"] = "Review saved.";
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdAsync(id, cancellationToken);
        if (review is null)
        {
            return NotFound();
        }

        if (!reviews.CanEdit(review, ActingUserFactory.Require(User)))
        {
            return Forbid();
        }

        await LoadCompaniesAsync(cancellationToken);
        return View(new InterviewReviewFormVm
        {
            ReviewId = review.ReviewId,
            CompanyId = review.CompanyId,
            Role = review.Role,
            Rating = review.Rating,
            Experience = review.Experience,
            WasLegit = review.WasLegit,
            ReviewDate = review.ReviewDate,
        });
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InterviewReviewFormVm form, CancellationToken cancellationToken)
    {
        form.ReviewId = id;
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await reviews.UpdateAsync(form, ActingUserFactory.Require(User), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The review could not be saved.");
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        TempData["Success"] = "Review updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdAsync(id, cancellationToken);
        if (review is null)
        {
            return NotFound();
        }

        if (!reviews.CanDelete(review, ActingUserFactory.Require(User)))
        {
            return Forbid();
        }

        return View(review);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.SignedIn)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await reviews.DeleteAsync(id, ActingUserFactory.Require(User), cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Review deleted." : result.ErrorMessage;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Delete), new { id });
    }

    private async Task LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
    }
}
