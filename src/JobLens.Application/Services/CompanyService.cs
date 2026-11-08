using JobLens.Application.Common;
using JobLens.Application.Interfaces;
using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

public sealed class CompanyService(ICompanyRepository companies)
{
    private const string ModeratorOnlyMessage = "Only moderators can create, edit, or delete companies.";

    public async Task<ServiceResult> CreateAsync(CompanyFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        if (!actor.IsModerator)
        {
            return ServiceResult.Fail(ModeratorOnlyMessage);
        }

        if (!TryNormalize(form, null, out var name, out var domain, out var industry, out var state, out var error))
        {
            return ServiceResult.Fail(error);
        }

        if (await companies.ExistsByNameAsync(name, null, cancellationToken))
        {
            return ServiceResult.Fail("A company with this name already exists.");
        }

        if (await companies.ExistsByDomainAsync(domain, null, cancellationToken))
        {
            return ServiceResult.Fail("A company with this domain already exists.");
        }

        var id = await companies.CreateAsync(name, domain, industry, state, cancellationToken);
        return ServiceResult.Ok(id);
    }

    public async Task<ServiceResult> UpdateAsync(CompanyFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        if (!actor.IsModerator)
        {
            return ServiceResult.Fail(ModeratorOnlyMessage);
        }

        if (await companies.GetByIdAsync(form.CompanyId, cancellationToken) is null)
        {
            return ServiceResult.Fail("Company not found.");
        }

        if (!TryNormalize(form, form.CompanyId, out var name, out var domain, out var industry, out var state, out var error))
        {
            return ServiceResult.Fail(error);
        }

        if (await companies.ExistsByNameAsync(name, form.CompanyId, cancellationToken))
        {
            return ServiceResult.Fail("A company with this name already exists.");
        }

        if (await companies.ExistsByDomainAsync(domain, form.CompanyId, cancellationToken))
        {
            return ServiceResult.Fail("A company with this domain already exists.");
        }

        var updated = await companies.UpdateAsync(form.CompanyId, name, domain, industry, state, cancellationToken);
        return updated ? ServiceResult.Ok(form.CompanyId) : ServiceResult.Fail("Company not found.");
    }

    public async Task<ServiceResult> DeleteAsync(int companyId, ActingUser actor, CancellationToken cancellationToken)
    {
        if (!actor.IsModerator)
        {
            return ServiceResult.Fail(ModeratorOnlyMessage);
        }

        if (await companies.GetByIdAsync(companyId, cancellationToken) is null)
        {
            return ServiceResult.Fail("Company not found.");
        }

        var dependents = await companies.CountDependentsAsync(companyId, cancellationToken);
        if (dependents > 0)
        {
            return ServiceResult.Fail($"This company has {dependents} related records and cannot be deleted.");
        }

        var deleted = await companies.DeleteAsync(companyId, cancellationToken);
        return deleted ? ServiceResult.Ok(companyId) : ServiceResult.Fail("Company not found.");
    }


    public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken) =>
        companies.GetAllAsync(cancellationToken);

    public Task<Company?> GetByIdAsync(int companyId, CancellationToken cancellationToken) =>
        companies.GetByIdAsync(companyId, cancellationToken);

    public Task<CompanyActivitySummary> GetActivityAsync(int companyId, CancellationToken cancellationToken) =>
        companies.GetActivityAsync(companyId, cancellationToken);

    public Task<int> CountDependentsAsync(int companyId, CancellationToken cancellationToken) =>
        companies.CountDependentsAsync(companyId, cancellationToken);

    public async Task<PageResult<Company>> BrowseAsync(string? search, int page, CancellationToken cancellationToken)
    {
        var all = await companies.GetAllAsync(cancellationToken);
        var term = search?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(term)
            ? all
            : all.Where(company =>
                company.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || company.OfficialDomain.Contains(term, StringComparison.OrdinalIgnoreCase)
                || company.Industry.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        var size = JobLens.Domain.Constants.Paging.PageSize;
        var index = Math.Max(page, 1);
        var items = filtered.Skip((index - 1) * size).Take(size).ToList();
        return new PageResult<Company>(items, index, size, filtered.Count);
    }

    private static bool TryNormalize(
        CompanyFormVm form,
        int? excludeId,
        out string name,
        out string domain,
        out string industry,
        out string state,
        out string error)
    {
        _ = excludeId;
        name = form.Name?.Trim() ?? "";
        domain = FieldRules.NormalizeDomain(form.OfficialDomain ?? "");
        industry = form.Industry?.Trim() ?? "";
        state = (form.HqState ?? "").Trim().ToUpperInvariant();
        if (name.Length is 0 or > 120)
        {
            error = "Company name is required.";
            return false;
        }

        if (domain.Length is 0 or > 100 || !domain.Contains('.', StringComparison.Ordinal))
        {
            error = "Enter an official domain such as amazon.com.";
            return false;
        }

        if (industry.Length is 0 or > 80)
        {
            error = "Industry is required.";
            return false;
        }

        if (state.Length != 2)
        {
            error = "State must be a 2-letter code.";
            return false;
        }

        error = "";
        return true;
    }
}
