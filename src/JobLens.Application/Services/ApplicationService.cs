using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Domain.Constants;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

public sealed class ApplicationService(
    IJobApplicationRepository applications,
    ICompanyRepository companies,
    RecruiterRiskService risk)
{
    public static bool IsFollowUpDue(JobApplication application, string? today = null)
    {
        if (string.IsNullOrWhiteSpace(application.NextFollowUpDate) || ApplicationStatuses.IsClosed(application.Status))
        {
            return false;
        }

        var day = today ?? FieldRules.Today();
        return string.CompareOrdinal(application.NextFollowUpDate, day) <= 0;
    }

    public async Task<PageResult<JobApplicationListItem>> BrowseAsync(
        ActingUser actor,
        string? status,
        string? search,
        int page,
        CancellationToken cancellationToken)
    {
        var result = await applications.BrowseAsync(actor.UserId, status, search, page, cancellationToken);
        var today = FieldRules.Today();
        var items = result.Items
            .Select(row => new JobApplicationListItem(
                row.ApplicationId,
                row.CompanyName,
                row.RoleTitle,
                row.Source,
                row.Status,
                row.AppliedDate,
                row.NextFollowUpDate,
                row.RecruiterEmail,
                row.RiskVerdict,
                IsFollowUpDue(row, today)))
            .ToList();
        return new PageResult<JobApplicationListItem>(items, result.Page, result.PageSize, result.Total);
    }

    public Task<JobApplication?> GetByIdAsync(int applicationId, ActingUser actor, CancellationToken cancellationToken) =>
        applications.GetByIdForUserAsync(applicationId, actor.UserId, cancellationToken);

    public async Task<JobApplicationPipeline> GetPipelineAsync(ActingUser actor, CancellationToken cancellationToken)
    {
        var counts = await applications.CountByStatusAsync(actor.UserId, cancellationToken);
        return BuildPipeline(counts);
    }

    public Task<int> CountAllAsync(CancellationToken cancellationToken) =>
        applications.CountAllAsync(cancellationToken);

    public async Task<IReadOnlyList<JobApplicationStatusCount>> GetStatusTabsAsync(
        ActingUser actor,
        CancellationToken cancellationToken)
    {
        var counts = await applications.CountByStatusAsync(actor.UserId, cancellationToken);
        var lookup = counts.ToDictionary(row => row.Status, row => row.Count, StringComparer.OrdinalIgnoreCase);
        return ApplicationStatuses.All
            .Select(status => new JobApplicationStatusCount(status, lookup.GetValueOrDefault(status)))
            .ToList();
    }

    public async Task<ServiceResult> CreateAsync(JobApplicationFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        if (actor.UserId <= 0)
        {
            return ServiceResult.Fail("You must be signed in to track applications.");
        }

        var (prepared, error) = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(error);
        }

        var now = FieldRules.Today();
        var id = await applications.CreateAsync(
            actor.UserId,
            prepared.CompanyName,
            prepared.CompanyId,
            prepared.RoleTitle,
            prepared.Source,
            prepared.JobUrl,
            prepared.RecruiterEmail,
            prepared.Status,
            prepared.AppliedDate,
            prepared.NextFollowUpDate,
            prepared.Notes,
            prepared.RiskVerdict,
            prepared.ClosedDate,
            now,
            now,
            cancellationToken);
        return ServiceResult.Ok(id);
    }

    public async Task<ServiceResult> UpdateAsync(JobApplicationFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        var existing = await applications.GetByIdForUserAsync(form.ApplicationId, actor.UserId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Application not found.");
        }

        var (prepared, error) = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(error);
        }

        var updated = await applications.UpdateAsync(
            form.ApplicationId,
            actor.UserId,
            prepared.CompanyName,
            prepared.CompanyId,
            prepared.RoleTitle,
            prepared.Source,
            prepared.JobUrl,
            prepared.RecruiterEmail,
            prepared.Status,
            prepared.AppliedDate,
            prepared.NextFollowUpDate,
            prepared.Notes,
            prepared.RiskVerdict,
            prepared.ClosedDate,
            FieldRules.Today(),
            cancellationToken);
        return updated ? ServiceResult.Ok(form.ApplicationId) : ServiceResult.Fail("Application not found.");
    }

    public async Task<ServiceResult> QuickStatusAsync(
        int applicationId,
        string status,
        ActingUser actor,
        CancellationToken cancellationToken)
    {
        var existing = await applications.GetByIdForUserAsync(applicationId, actor.UserId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Application not found.");
        }

        var normalized = status?.Trim() ?? "";
        if (!ApplicationStatuses.All.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return ServiceResult.Fail("Choose a valid application status.");
        }

        var closed = ApplicationStatuses.IsClosed(normalized) ? FieldRules.Today() : null;
        var updated = await applications.UpdateStatusAsync(
            applicationId,
            actor.UserId,
            normalized,
            closed,
            FieldRules.Today(),
            cancellationToken);
        return updated ? ServiceResult.Ok(applicationId) : ServiceResult.Fail("Application not found.");
    }

    public async Task<ServiceResult> DeleteAsync(int applicationId, ActingUser actor, CancellationToken cancellationToken)
    {
        var existing = await applications.GetByIdForUserAsync(applicationId, actor.UserId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Application not found.");
        }

        var deleted = await applications.DeleteAsync(applicationId, actor.UserId, cancellationToken);
        return deleted ? ServiceResult.Ok(applicationId) : ServiceResult.Fail("Application not found.");
    }

    internal static JobApplicationPipeline BuildPipeline(IReadOnlyList<JobApplicationStatusCount> counts)
    {
        var lookup = counts.ToDictionary(row => row.Status, row => row.Count, StringComparer.OrdinalIgnoreCase);
        int Of(string status) => lookup.GetValueOrDefault(status);

        var applied = Of(ApplicationStatuses.Applied);
        var interviewing = Of(ApplicationStatuses.Interviewing);
        var offers = Of(ApplicationStatuses.Offer);
        var rejected = Of(ApplicationStatuses.Rejected);
        var withdrawn = Of(ApplicationStatuses.Withdrawn);
        var noResponse = Of(ApplicationStatuses.NoResponse);
        var responded = interviewing + offers + rejected;
        var submitted = applied + interviewing + offers + rejected + withdrawn + noResponse;
        var rate = submitted == 0 ? 0 : Math.Round(responded * 100.0 / submitted, 1);

        var byStatus = ApplicationStatuses.All
            .Select(status => new JobApplicationStatusCount(status, Of(status)))
            .ToList();
        return new JobApplicationPipeline(applied, interviewing, offers, rate, byStatus);
    }

    private async Task<(Prepared? Prepared, string Error)> TryPrepareAsync(
        JobApplicationFormVm form,
        CancellationToken cancellationToken)
    {
        var companyName = form.CompanyName?.Trim() ?? "";
        var roleTitle = form.RoleTitle?.Trim() ?? "";
        var source = form.Source?.Trim() ?? "";
        var status = form.Status?.Trim() ?? "";
        var jobUrl = string.IsNullOrWhiteSpace(form.JobUrl) ? null : form.JobUrl.Trim();
        var recruiterEmail = string.IsNullOrWhiteSpace(form.RecruiterEmail) ? null : form.RecruiterEmail.Trim();
        var appliedDate = string.IsNullOrWhiteSpace(form.AppliedDate) ? null : form.AppliedDate.Trim();
        var nextFollowUp = string.IsNullOrWhiteSpace(form.NextFollowUpDate) ? null : form.NextFollowUpDate.Trim();
        var notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim();

        if (companyName.Length is < 2 or > 120)
        {
            return (null, "Company name must be 2 to 120 characters.");
        }

        if (roleTitle.Length is < 2 or > 120)
        {
            return (null, "Role title must be 2 to 120 characters.");
        }

        if (!ApplicationSources.All.Contains(source, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "Choose a valid application source.");
        }

        if (!ApplicationStatuses.All.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "Choose a valid application status.");
        }

        if (jobUrl is not null && !IsHttpUrl(jobUrl))
        {
            return (null, "Job URL must start with http:// or https://.");
        }

        if (recruiterEmail is not null && !FieldRules.IsEmail(recruiterEmail))
        {
            return (null, "Enter a valid recruiter email, or leave it blank.");
        }

        if (appliedDate is not null)
        {
            if (!FieldRules.IsDate(appliedDate))
            {
                return (null, "Applied date must be yyyy-MM-dd.");
            }

            if (string.CompareOrdinal(appliedDate, FieldRules.Today()) > 0)
            {
                return (null, "Applied date cannot be in the future.");
            }
        }

        if (nextFollowUp is not null)
        {
            if (!FieldRules.IsDate(nextFollowUp))
            {
                return (null, "Follow-up date must be yyyy-MM-dd.");
            }

            if (appliedDate is not null && string.CompareOrdinal(nextFollowUp, appliedDate) < 0)
            {
                return (null, "Follow-up date cannot be before the applied date.");
            }
        }

        if (notes is not null && notes.Length > 1000)
        {
            return (null, "Notes cannot exceed 1000 characters.");
        }

        var companyId = await ResolveCompanyIdAsync(companyName, cancellationToken);
        string? riskVerdict = null;
        if (recruiterEmail is not null)
        {
            var check = await risk.CheckAsync(recruiterEmail, companyName, cancellationToken);
            riskVerdict = ApplicationRiskVerdicts.FromRecruiterVerdict(check.Verdict);
        }

        var closedDate = ApplicationStatuses.IsClosed(status) ? FieldRules.Today() : null;
        return (new Prepared(
            companyName,
            companyId,
            roleTitle,
            source,
            jobUrl,
            recruiterEmail,
            status,
            appliedDate,
            nextFollowUp,
            notes,
            riskVerdict,
            closedDate), "");
    }

    private async Task<int?> ResolveCompanyIdAsync(string companyName, CancellationToken cancellationToken)
    {
        var all = await companies.GetAllAsync(cancellationToken);
        var match = all.FirstOrDefault(company =>
            company.Name.Equals(companyName, StringComparison.OrdinalIgnoreCase));
        return match?.CompanyId;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private sealed record Prepared(
        string CompanyName,
        int? CompanyId,
        string RoleTitle,
        string Source,
        string? JobUrl,
        string? RecruiterEmail,
        string Status,
        string? AppliedDate,
        string? NextFollowUpDate,
        string? Notes,
        string? RiskVerdict,
        string? ClosedDate);
}
