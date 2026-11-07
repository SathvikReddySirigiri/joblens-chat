using JobLens.Application.Common;
using JobLens.Application.Interfaces;
using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

public sealed class JobPostingService(IJobPostingRepository postings, ICompanyRepository companies)
{
    public async Task<ServiceResult> CreateAsync(JobPostingFormVm form, CancellationToken cancellationToken)
    {
        var prepared = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(_error);
        }

        var id = await postings.CreateAsync(
            prepared.CompanyId, prepared.Title, prepared.Location, prepared.State,
            prepared.IsRemote, prepared.IsPaid, prepared.Pay, prepared.Description, prepared.PostedDate,
            cancellationToken);
        return ServiceResult.Ok(id);
    }

    public async Task<ServiceResult> UpdateAsync(JobPostingFormVm form, CancellationToken cancellationToken)
    {
        if (await postings.GetByIdAsync(form.PostingId, cancellationToken) is null)
        {
            return ServiceResult.Fail("Posting not found.");
        }

        var prepared = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(_error);
        }

        var updated = await postings.UpdateAsync(
            form.PostingId, prepared.CompanyId, prepared.Title, prepared.Location, prepared.State,
            prepared.IsRemote, prepared.IsPaid, prepared.Pay, prepared.Description, prepared.PostedDate,
            cancellationToken);
        return updated ? ServiceResult.Ok(form.PostingId) : ServiceResult.Fail("Posting not found.");
    }

    public async Task<ServiceResult> DeleteAsync(int postingId, CancellationToken cancellationToken)
    {
        var deleted = await postings.DeleteAsync(postingId, cancellationToken);
        return deleted ? ServiceResult.Ok(postingId) : ServiceResult.Fail("Posting not found.");
    }

    private string _error = "";

    private async Task<Prepared?> TryPrepareAsync(JobPostingFormVm form, CancellationToken cancellationToken)
    {
        var title = form.Title?.Trim() ?? "";
        var location = form.Location?.Trim() ?? "";
        var state = (form.State ?? "").Trim().ToUpperInvariant();
        var description = form.Description?.Trim() ?? "";
        var posted = string.IsNullOrWhiteSpace(form.PostedDate) ? FieldRules.Today() : form.PostedDate.Trim();
        if (title.Length is 0 or > 120 || location.Length is 0 or > 120)
        {
            _error = "Title and location are required.";
            return null;
        }

        if (state.Length != 2)
        {
            _error = "State must be a 2-letter code.";
            return null;
        }

        if (description.Length is < 20 or > 2000)
        {
            _error = "Description must be 20 to 2000 characters.";
            return null;
        }

        if (!FieldRules.IsDate(posted))
        {
            _error = "Posted date must be yyyy-MM-dd.";
            return null;
        }

        double? pay;
        if (form.IsPaid)
        {
            if (form.PayRatePerHour is null or < 0)
            {
                _error = "A paid posting needs a pay rate of zero or more.";
                return null;
            }

            pay = form.PayRatePerHour;
        }
        else if (form.PayRatePerHour is > 0)
        {
            _error = "An unpaid posting cannot include a pay rate. Leave it empty or zero.";
            return null;
        }
        else
        {
            pay = null;
        }

        if (await companies.GetByIdAsync(form.CompanyId, cancellationToken) is null)
        {
            _error = "Company not found.";
            return null;
        }

        return new Prepared(form.CompanyId, title, location, state, form.IsRemote, form.IsPaid, pay, description, posted);
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
