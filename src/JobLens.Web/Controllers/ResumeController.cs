using JobLens.Application.Dtos;
using JobLens.Application.Services;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobLens.Web.Controllers;

[Authorize(Policy = AuthPolicies.SignedIn)]
public sealed class ResumeController(ResumeService resumes) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (page, _) = await resumes.GetPageAsync(ActingUserFactory.Require(User), cancellationToken);
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2_200_000)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        var actor = ActingUserFactory.Require(User);
        if (file is null || file.Length == 0)
        {
            var (emptyPage, _) = await resumes.GetPageAsync(actor, cancellationToken);
            return View("Index", emptyPage with { UploadError = "Choose a PDF or Word file." });
        }

        if (file.Length > ResumeService.MaxUploadBytes)
        {
            var (overPage, _) = await resumes.GetPageAsync(actor, cancellationToken);
            return View("Index", overPage with { UploadError = "The file must be 2 MB or smaller." });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var (success, error, page) = await resumes.UploadAsync(actor, file.FileName, buffer.ToArray(), cancellationToken);
        if (!success || page is null)
        {
            var (fallback, _) = await resumes.GetPageAsync(actor, cancellationToken);
            return View("Index", fallback with { UploadError = error ?? "Upload failed." });
        }

        TempData["Success"] = "Resume scanned. Only redacted text was saved — the original file was not stored.";
        return View("Index", page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        var deleted = await resumes.DeleteAsync(ActingUserFactory.Require(User), cancellationToken);
        TempData["Success"] = deleted ? "Your resume was deleted." : "No resume to delete.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Match(
        string? jobTitle,
        string? companyName,
        string? jobText,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var actor = ActingUserFactory.Require(User);
        var text = string.Join(
            "\n",
            new[] { jobTitle, companyName, jobText }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var match = await resumes.MatchForUserAsync(actor, text, cancellationToken);
        if (match is null)
        {
            TempData["Error"] = "Upload a resume first to match skills.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.JobTitle = jobTitle;
        ViewBag.CompanyName = companyName;
        ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Action(nameof(Index)) : returnUrl;
        return View(match);
    }
}
