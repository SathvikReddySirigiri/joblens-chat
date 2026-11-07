using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Domain.Constants;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

public sealed class ScamReportService(IScamReportRepository reports, ICompanyRepository companies)
{
    private static readonly HashSet<string> ScamTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fake check", "Equipment purchase", "Upfront fee", "Chat-only interview", "Identity theft", "Fake staffing agency",
    };

    private static readonly HashSet<string> Channels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email", "Telegram", "WhatsApp", "LinkedIn", "Text message",
    };

    public async Task<ServiceResult> CreateAsync(ScamReportFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        if (actor.UserId <= 0)
        {
            return ServiceResult.Fail("You must be signed in to create a report.");
        }

        var prepared = await TryPrepareAsync(form, null, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(_error);
        }

        var id = await reports.CreateAsync(
            prepared.CompanyId,
            prepared.Email,
            prepared.Domain,
            prepared.ScamType,
            prepared.Channel,
            prepared.Amount,
            prepared.Age,
            ReportStatus.Pending,
            prepared.ReportedDate,
            prepared.Description,
            actor.UserId,
            cancellationToken);
        return ServiceResult.Ok(id);
    }

    public async Task<ServiceResult> UpdateAsync(ScamReportFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        var existing = await reports.GetByIdAsync(form.ReportId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Report not found.");
        }

        if (!CanEdit(existing, actor))
        {
            return ServiceResult.Fail(EditDeniedMessage(existing, actor));
        }

        var prepared = await TryPrepareAsync(form, form.ReportId, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(_error);
        }

        var updated = await reports.UpdateAsync(
            form.ReportId,
            prepared.CompanyId,
            prepared.Email,
            prepared.Domain,
            prepared.ScamType,
            prepared.Channel,
            prepared.Amount,
            prepared.Age,
            prepared.Description,
            prepared.ReportedDate,
            cancellationToken);
        return updated ? ServiceResult.Ok(form.ReportId) : ServiceResult.Fail("Report not found.");
    }

    public Task<ServiceResult> ApproveAsync(int reportId, ActingUser actor, CancellationToken cancellationToken) =>
        ChangeStatusAsync(reportId, ReportStatus.Approved, actor, cancellationToken);

    public Task<ServiceResult> RejectAsync(int reportId, ActingUser actor, CancellationToken cancellationToken) =>
        ChangeStatusAsync(reportId, ReportStatus.Rejected, actor, cancellationToken);

    public async Task<ServiceResult> DeleteAsync(int reportId, ActingUser actor, CancellationToken cancellationToken)
    {
        var existing = await reports.GetByIdAsync(reportId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Report not found.");
        }

        if (!CanDelete(existing, actor))
        {
            return ServiceResult.Fail(DeleteDeniedMessage(existing, actor));
        }

        var deleted = await reports.DeleteAsync(reportId, cancellationToken);
        return deleted ? ServiceResult.Ok(reportId) : ServiceResult.Fail("Report not found.");
    }

    public bool CanView(ScamReport report, ActingUser? viewer)
    {
        if (viewer?.IsModerator == true)
        {
            return true;
        }

        if (string.Equals(report.Status, ReportStatus.Approved, StringComparison.Ordinal))
        {
            return true;
        }

        return viewer is not null && report.CreatedByUserId == viewer.UserId;
    }

    public bool CanEdit(ScamReport report, ActingUser? actor)
    {
        if (actor is null)
        {
            return false;
        }

        if (actor.IsModerator)
        {
            return true;
        }

        return actor.IsStudent
            && report.CreatedByUserId == actor.UserId
            && string.Equals(report.Status, ReportStatus.Pending, StringComparison.Ordinal);
    }

    public bool CanDelete(ScamReport report, ActingUser? actor) => CanEdit(report, actor);

    public bool CanModerate(ActingUser? actor) => actor?.IsModerator == true;

    private string _error = "";

    private async Task<Prepared?> TryPrepareAsync(ScamReportFormVm form, int? excludeId, CancellationToken cancellationToken)
    {
        var email = (form.RecruiterEmail ?? "").Trim().ToLowerInvariant();
        var description = (form.Description ?? "").Trim();
        var scamType = Canonical(ScamTypes, form.ScamType);
        var channel = Canonical(Channels, form.Channel);
        if (!FieldRules.IsEmail(email))
        {
            _error = "Enter a valid recruiter email address.";
            return null;
        }

        var domain = FieldRules.DomainOfEmail(email);
        if (domain.Length == 0)
        {
            _error = "Enter a valid recruiter email address.";
            return null;
        }

        if (scamType is null)
        {
            _error = "Choose a scam type.";
            return null;
        }

        if (channel is null)
        {
            _error = "Choose a channel.";
            return null;
        }

        if (description.Length is < 20 or > 500)
        {
            _error = "Description must be 20 to 500 characters.";
            return null;
        }

        if (form.AmountRequestedUsd < 0 || form.DomainAgeDays < 0)
        {
            _error = "Amount and domain age cannot be negative.";
            return null;
        }

        if (await companies.GetByIdAsync(form.CompanyId, cancellationToken) is null)
        {
            _error = "Company not found.";
            return null;
        }

        var reported = string.IsNullOrWhiteSpace(form.ReportedDate) ? FieldRules.Today() : form.ReportedDate.Trim();
        if (!FieldRules.IsDate(reported))
        {
            _error = "Reported date must be yyyy-MM-dd.";
            return null;
        }

        var duplicateId = await reports.FindDuplicateIdAsync(email, scamType, excludeId, cancellationToken);
        if (duplicateId is not null)
        {
            _error = $"A report with this recruiter email and scam type already exists (report {duplicateId.Value}).";
            return null;
        }

        return new Prepared(form.CompanyId, email, domain, scamType, channel, form.AmountRequestedUsd, form.DomainAgeDays, description, reported);
    }

    private async Task<ServiceResult> ChangeStatusAsync(
        int reportId,
        string status,
        ActingUser actor,
        CancellationToken cancellationToken)
    {
        if (!actor.IsModerator)
        {
            return ServiceResult.Fail("Only moderators can approve or reject reports.");
        }

        var existing = await reports.GetByIdAsync(reportId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Report not found.");
        }

        if (!string.Equals(existing.Status, ReportStatus.Pending, StringComparison.Ordinal))
        {
            return ServiceResult.Fail("Only pending reports can be approved or rejected.");
        }

        var updated = await reports.SetStatusAsync(reportId, status, cancellationToken);
        return updated ? ServiceResult.Ok(reportId) : ServiceResult.Fail("Report not found.");
    }

    private static string EditDeniedMessage(ScamReport report, ActingUser actor)
    {
        if (actor.IsStudent && report.CreatedByUserId == actor.UserId
            && !string.Equals(report.Status, ReportStatus.Pending, StringComparison.Ordinal))
        {
            return "You can only edit your own reports while they are still Pending.";
        }

        return "You can only edit your own pending reports.";
    }

    private static string DeleteDeniedMessage(ScamReport report, ActingUser actor)
    {
        if (actor.IsStudent && report.CreatedByUserId == actor.UserId
            && !string.Equals(report.Status, ReportStatus.Pending, StringComparison.Ordinal))
        {
            return "You can only delete your own reports while they are still Pending.";
        }

        return "You can only delete your own pending reports.";
    }

    private static string? Canonical(HashSet<string> allowed, string? value)
    {
        var text = value?.Trim() ?? "";
        foreach (var item in allowed)
        {
            if (string.Equals(item, text, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private sealed record Prepared(
        int CompanyId,
        string Email,
        string Domain,
        string ScamType,
        string Channel,
        double Amount,
        int Age,
        string Description,
        string ReportedDate);

    public Task<PageResult<ScamReport>> BrowseAsync(
        string? search,
        string? status,
        string? scamType,
        string? channel,
        int? companyId,
        string? sort,
        bool descending,
        int page,
        ActingUser? viewer,
        CancellationToken cancellationToken) =>
        reports.BrowseAsync(
            search,
            status,
            scamType,
            channel,
            companyId,
            sort,
            descending,
            page,
            viewer?.UserId ?? 0,
            viewer?.IsModerator == true,
            cancellationToken);

    public Task<ScamReport?> GetByIdAsync(int reportId, CancellationToken cancellationToken) =>
        reports.GetByIdAsync(reportId, cancellationToken);

    public Task<IReadOnlyList<ScamReport>> OthersOnDomainAsync(
        string domain,
        int excludeId,
        int limit,
        ActingUser? viewer,
        CancellationToken cancellationToken) =>
        reports.OthersOnDomainAsync(
            domain,
            excludeId,
            limit,
            viewer?.UserId ?? 0,
            viewer?.IsModerator == true,
            cancellationToken);

    public Task<int> CountByStatusAsync(string status, CancellationToken cancellationToken) =>
        reports.CountByStatusAsync(status, cancellationToken);
}
