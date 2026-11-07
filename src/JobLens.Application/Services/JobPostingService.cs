using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

public sealed class JobPostingService(IJobPostingRepository postings, ICompanyRepository companies)
{
    private const string ModeratorOnlyMessage = "Only moderators can create, edit, or delete job postings.";

    public async Task<ServiceResult> CreateAsync(JobPostingFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        if (!actor.IsModerator)
        {
            return ServiceResult.Fail(ModeratorOnlyMessage);
        }

        var (prepared, error) = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(error);
        }

        var id = await postings.CreateAsync(
            prepared.CompanyId, prepared.Title, prepared.Location, prepared.State,
            prepared.IsRemote, prepared.IsPaid, prepared.Pay, prepared.Description, prepared.PostedDate,
            cancellationToken);
        return ServiceResult.Ok(id);
    }

    public async Task<ServiceResult> UpdateAsync(JobPostingFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        if (!actor.IsModerator)
        {
            return ServiceResult.Fail(ModeratorOnlyMessage);
        }

        if (await postings.GetByIdAsync(form.PostingId, cancellationToken) is null)
        {
            return ServiceResult.Fail("Posting not found.");
        }

        var (prepared, error) = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(error);
        }

        var updated = await postings.UpdateAsync(
            form.PostingId, prepared.CompanyId, prepared.Title, prepared.Location, prepared.State,
            prepared.IsRemote, prepared.IsPaid, prepared.Pay, prepared.Description, prepared.PostedDate,
            cancellationToken);
        return updated ? ServiceResult.Ok(form.PostingId) : ServiceResult.Fail("Posting not found.");
    }

    public async Task<ServiceResult> DeleteAsync(int postingId, ActingUser actor, CancellationToken cancellationToken)
    {
        if (!actor.IsModerator)
        {
            return ServiceResult.Fail(ModeratorOnlyMessage);
        }

        var deleted = await postings.DeleteAsync(postingId, cancellationToken);
        return deleted ? ServiceResult.Ok(postingId) : ServiceResult.Fail("Posting not found.");
    }

    private async Task<(Prepared? Prepared, string Error)> TryPrepareAsync(
        JobPostingFormVm form,
        CancellationToken cancellationToken)
    {
        var title = form.Title?.Trim() ?? "";
        var location = form.Location?.Trim() ?? "";
        var state = (form.State ?? "").Trim().ToUpperInvariant();
        var description = form.Description?.Trim() ?? "";
        var posted = string.IsNullOrWhiteSpace(form.PostedDate) ? FieldRules.Today() : form.PostedDate.Trim();
        if (title.Length is 0 or > 120 || location.Length is 0 or > 120)
        {
            return (null, "Title and location are required.");
        }

        if (state.Length != 2)
        {
            return (null, "State must be a 2-letter code.");
        }

        if (description.Length is < 20 or > 2000)
        {
            return (null, "Description must be 20 to 2000 characters.");
        }

        if (!FieldRules.IsDate(posted))
        {
            return (null, "Posted date must be yyyy-MM-dd.");
        }

        double? pay;
        if (form.IsPaid)
        {
            if (form.PayRatePerHour is null or < 0)
            {
                return (null, "A paid posting needs a pay rate of zero or more.");
            }

            pay = form.PayRatePerHour;
        }
        else if (form.PayRatePerHour is > 0)
        {
            return (null, "An unpaid posting cannot include a pay rate. Leave it empty or zero.");
        }
        else
        {
            pay = null;
        }

        if (await companies.GetByIdAsync(form.CompanyId, cancellationToken) is null)
        {
            return (null, "Company not found.");
        }

        return (new Prepared(form.CompanyId, title, location, state, form.IsRemote, form.IsPaid, pay, description, posted), "");
    }

    private sealed record Prepared(
        int CompanyId,
        string Title,
        string Location,
        string State,
        bool IsRemote,
        bool IsPaid,
        double? Pay,
        string Description,
        string PostedDate);

    public Task<PageResult<JobPosting>> SearchPageAsync(SearchFilter filter, int page, CancellationToken cancellationToken) =>
        postings.SearchPageAsync(filter, page, cancellationToken);

    public Task<JobPosting?> GetByIdAsync(int postingId, CancellationToken cancellationToken) =>
        postings.GetByIdAsync(postingId, cancellationToken);
}
