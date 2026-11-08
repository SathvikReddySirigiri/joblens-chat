using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Pages;
using JobLens.Application.Services;
using JobLens.Domain.Constants;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobLens.Web.Controllers;

[Authorize(Policy = AuthPolicies.SignedIn)]
public sealed class EmailDraftsController(EmailDraftService drafts, ApplicationService applications) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? applicationId, CancellationToken cancellationToken)
    {
        var actor = ActingUserFactory.Require(User);
        var page = new EmailDraftPageVm
        {
            StudentName = User.Identity?.Name ?? "",
            Applications = await LoadOptionsAsync(actor, cancellationToken),
        };

        if (applicationId is int id)
        {
            var app = await applications.GetByIdAsync(id, actor, cancellationToken);
            if (app is null)
            {
                return NotFound();
            }

            Prefill(page, app.ApplicationId, app.CompanyName, app.RoleTitle, app.RecruiterEmail);
        }

        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(LoginRateLimiting.ExpensivePolicyName)]
    public async Task<IActionResult> Index(EmailDraftPageVm form, CancellationToken cancellationToken)
    {
        var actor = ActingUserFactory.Require(User);
        form.Applications = await LoadOptionsAsync(actor, cancellationToken);

        if (form.PrefillApplicationId is int selectedId && Request.Form.ContainsKey("prefill"))
        {
            var app = await applications.GetByIdAsync(selectedId, actor, cancellationToken);
            if (app is null)
            {
                return NotFound();
            }

            Prefill(form, app.ApplicationId, app.CompanyName, app.RoleTitle, app.RecruiterEmail);
            return View(form);
        }

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await drafts.DraftAsync(
            new EmailDraftRequest(
                form.Type,
                form.PrefillApplicationId,
                form.RecruiterName,
                form.RecruiterEmail,
                form.CompanyName,
                form.RoleTitle,
                form.StudentName,
                form.Tone,
                form.ExtraNotes),
            actor,
            cancellationToken);

        if (result.NotFound)
        {
            return NotFound();
        }

        if (!result.Success || result.Draft is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The draft could not be created.");
            return View(form);
        }

        form.Result = result.Draft;
        form.MailtoUrl = EmailDraftService.BuildMailto(form.RecruiterEmail, result.Draft.Subject, result.Draft.Body);
        form.Type = result.Draft.EffectiveType;
        return View(form);
    }

    private async Task<IReadOnlyList<EmailDraftApplicationOption>> LoadOptionsAsync(
        ActingUser actor,
        CancellationToken cancellationToken)
    {
        var page = await applications.BrowseAsync(actor, status: null, search: null, page: 1, cancellationToken);
        // Load a few pages worth for the dropdown without a dedicated list API.
        var items = page.Items.ToList();
        if (page.Total > page.PageSize)
        {
            var second = await applications.BrowseAsync(actor, null, null, 2, cancellationToken);
            items.AddRange(second.Items);
        }

        return items
            .Select(item => new EmailDraftApplicationOption(
                item.ApplicationId,
                $"{item.CompanyName} — {item.RoleTitle} ({item.Status})",
                item.CompanyName,
                item.RoleTitle,
                item.RecruiterEmail,
                null))
            .ToList();
    }

    private static void Prefill(
        EmailDraftPageVm form,
        int applicationId,
        string company,
        string role,
        string? recruiterEmail)
    {
        form.PrefillApplicationId = applicationId;
        form.CompanyName = company;
        form.RoleTitle = role;
        form.RecruiterEmail = recruiterEmail;
    }
}
