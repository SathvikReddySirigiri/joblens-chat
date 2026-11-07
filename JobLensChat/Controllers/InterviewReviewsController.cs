using JobLensChat.Data;
using JobLensChat.Services;
using JobLensChat.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class InterviewReviewsController(
    InterviewReviewService reviews,
    InterviewReviewRepository repository,
    CompanyRepository companies) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? company, int? rating, bool? legit, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewBag.Page = await repository.BrowseAsync(company, rating, legit, page, cancellationToken);
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
        ViewBag.Company = company;
        ViewBag.Rating = rating;
        ViewBag.Legit = legit;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var review = await repository.GetByIdAsync(id, cancellationToken);
        return review is null ? NotFound() : View(review);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadCompaniesAsync(cancellationToken);
        return View(new InterviewReviewFormVm { ReviewDate = FieldRules.Today(), Rating = 5, WasLegit = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InterviewReviewFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await reviews.CreateAsync(form, cancellationToken);
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
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var review = await repository.GetByIdAsync(id, cancellationToken);
        if (review is null)
        {
            return NotFound();
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InterviewReviewFormVm form, CancellationToken cancellationToken)
    {
        form.ReviewId = id;
        if (!ModelState.IsValid)
        {
            await LoadCompaniesAsync(cancellationToken);
            return View(form);
        }

        var result = await reviews.UpdateAsync(form, cancellationToken);
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
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var review = await repository.GetByIdAsync(id, cancellationToken);
        return review is null ? NotFound() : View(review);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await reviews.DeleteAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Review deleted." : result.ErrorMessage;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Delete), new { id });
    }

    private async Task LoadCompaniesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Companies = await companies.GetAllAsync(cancellationToken);
    }
}
