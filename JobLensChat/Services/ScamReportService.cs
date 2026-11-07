using JobLensChat.Data;
using JobLensChat.ViewModels;

namespace JobLensChat.Services;

public sealed class ScamReportService(ScamReportRepository reports, CompanyRepository companies)
{
    private static readonly HashSet<string> ScamTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fake check", "Equipment purchase", "Upfront fee", "Chat-only interview", "Identity theft", "Fake staffing agency",
    };

    private static readonly HashSet<string> Channels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email", "Telegram", "WhatsApp", "LinkedIn", "Text message",
    };

    public async Task<ServiceResult> CreateAsync(ScamReportFormVm form, CancellationToken cancellationToken)
    {
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
            "Pending",
            FieldRules.Today(),
            prepared.Description,
            cancellationToken);
        return ServiceResult.Ok(id);
    }

    public async Task<ServiceResult> UpdateAsync(ScamReportFormVm form, CancellationToken cancellationToken)
    {
        if (await reports.GetByIdAsync(form.ReportId, cancellationToken) is null)
        {
            return ServiceResult.Fail("Report not found.");
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
            cancellationToken);
        return updated ? ServiceResult.Ok(form.ReportId) : ServiceResult.Fail("Report not found.");
    }

    public Task<ServiceResult> ApproveAsync(int reportId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(reportId, "Approved", cancellationToken);

    public Task<ServiceResult> RejectAsync(int reportId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(reportId, "Rejected", cancellationToken);

    public async Task<ServiceResult> DeleteAsync(int reportId, CancellationToken cancellationToken)
    {
        var deleted = await reports.DeleteAsync(reportId, cancellationToken);
        return deleted ? ServiceResult.Ok(reportId) : ServiceResult.Fail("Report not found.");
    }

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

        if (await reports.ExistsDuplicateAsync(email, scamType, excludeId, cancellationToken))
        {
            _error = "A report with this recruiter email and scam type already exists.";
            return null;
        }

        return new Prepared(form.CompanyId, email, domain, scamType, channel, form.AmountRequestedUsd, form.DomainAgeDays, description);
    }

    private async Task<ServiceResult> ChangeStatusAsync(int reportId, string status, CancellationToken cancellationToken)
    {
        var existing = await reports.GetByIdAsync(reportId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Report not found.");
        }

        if (!string.Equals(existing.Status, "Pending", StringComparison.Ordinal))
        {
            return ServiceResult.Fail("Only pending reports can be approved or rejected.");
        }

        var updated = await reports.SetStatusAsync(reportId, status, cancellationToken);
        return updated ? ServiceResult.Ok(reportId) : ServiceResult.Fail("Report not found.");
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
        string Description);
}
