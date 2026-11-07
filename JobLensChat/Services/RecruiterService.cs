using JobLensChat.Data;
using JobLensChat.ViewModels;

namespace JobLensChat.Services;

public sealed class RecruiterService(RecruiterRepository recruiters)
{
    public async Task<ServiceResult> CreateAsync(RecruiterFormVm form, CancellationToken cancellationToken)
    {
        if (!TryNormalize(form, out var email, out var domain, out var firstSeen, out var notes, out var error))
        {
            return ServiceResult.Fail(error);
        }

        if (await recruiters.EmailExistsAsync(email, null, cancellationToken))
        {
            return ServiceResult.Fail("A recruiter with this email already exists.");
        }

        var id = await recruiters.CreateAsync(email, domain, CompanyId(form), firstSeen, notes, cancellationToken);
        return ServiceResult.Ok(id);
    }

    public async Task<ServiceResult> UpdateAsync(RecruiterFormVm form, CancellationToken cancellationToken)
    {
        if (await recruiters.GetByIdAsync(form.RecruiterId, cancellationToken) is null)
        {
            return ServiceResult.Fail("Recruiter not found.");
        }

        if (!TryNormalize(form, out var email, out var domain, out var firstSeen, out var notes, out var error))
        {
            return ServiceResult.Fail(error);
        }

        if (await recruiters.EmailExistsAsync(email, form.RecruiterId, cancellationToken))
        {
            return ServiceResult.Fail("A recruiter with this email already exists.");
        }

        var updated = await recruiters.UpdateAsync(form.RecruiterId, email, domain, CompanyId(form), firstSeen, notes, cancellationToken);
        return updated ? ServiceResult.Ok(form.RecruiterId) : ServiceResult.Fail("Recruiter not found.");
    }

    public async Task<ServiceResult> DeleteAsync(int recruiterId, CancellationToken cancellationToken)
    {
        var deleted = await recruiters.DeleteAsync(recruiterId, cancellationToken);
        return deleted ? ServiceResult.Ok(recruiterId) : ServiceResult.Fail("Recruiter not found.");
    }

    private static int? CompanyId(RecruiterFormVm form) => form.CompanyId is > 0 ? form.CompanyId : null;

    private static bool TryNormalize(
        RecruiterFormVm form,
        out string email,
        out string domain,
        out string firstSeen,
        out string notes,
        out string error)
    {
        email = (form.Email ?? "").Trim().ToLowerInvariant();
        domain = FieldRules.DomainOfEmail(email);
        firstSeen = string.IsNullOrWhiteSpace(form.FirstSeen) ? FieldRules.Today() : form.FirstSeen.Trim();
        notes = (form.Notes ?? "").Trim();
        if (!FieldRules.IsEmail(email) || domain.Length == 0)
        {
            error = "Enter a valid recruiter email address.";
            return false;
        }

        if (!FieldRules.IsDate(firstSeen))
        {
            error = "First seen must be a yyyy-MM-dd date.";
            return false;
        }

        if (notes.Length > 500)
        {
            error = "Notes must be 500 characters or fewer.";
            return false;
        }

        error = "";
        return true;
    }
}
