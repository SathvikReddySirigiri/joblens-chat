using JobLensChat.Data;
using JobLensChat.ViewModels;

namespace JobLensChat.Services;

public sealed class InterviewReviewService(InterviewReviewRepository reviews, CompanyRepository companies)
{
    public async Task<ServiceResult> CreateAsync(InterviewReviewFormVm form, CancellationToken cancellationToken)
    {
        var prepared = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(_error);
        }

        var id = await reviews.CreateAsync(
            prepared.CompanyId, prepared.Role, prepared.Rating, prepared.Experience, prepared.WasLegit, prepared.ReviewDate,
            cancellationToken);
        return ServiceResult.Ok(id);
    }

    public async Task<ServiceResult> UpdateAsync(InterviewReviewFormVm form, CancellationToken cancellationToken)
    {
        if (await reviews.GetByIdAsync(form.ReviewId, cancellationToken) is null)
        {
            return ServiceResult.Fail("Review not found.");
        }

        var prepared = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(_error);
        }

        var updated = await reviews.UpdateAsync(
            form.ReviewId, prepared.CompanyId, prepared.Role, prepared.Rating, prepared.Experience, prepared.WasLegit, prepared.ReviewDate,
            cancellationToken);
        return updated ? ServiceResult.Ok(form.ReviewId) : ServiceResult.Fail("Review not found.");
    }

    public async Task<ServiceResult> DeleteAsync(int reviewId, CancellationToken cancellationToken)
    {
        var deleted = await reviews.DeleteAsync(reviewId, cancellationToken);
        return deleted ? ServiceResult.Ok(reviewId) : ServiceResult.Fail("Review not found.");
    }

    private string _error = "";

    private async Task<Prepared?> TryPrepareAsync(InterviewReviewFormVm form, CancellationToken cancellationToken)
    {
        var role = form.Role?.Trim() ?? "";
        var experience = form.Experience?.Trim() ?? "";
        var reviewDate = string.IsNullOrWhiteSpace(form.ReviewDate) ? FieldRules.Today() : form.ReviewDate.Trim();
        if (role.Length is 0 or > 120)
        {
            _error = "Role is required.";
            return null;
        }

        if (form.Rating is < 1 or > 5)
        {
            _error = "Rating must be from 1 to 5.";
            return null;
        }

        if (experience.Length is < 20 or > 2000)
        {
            _error = "Experience must be 20 to 2000 characters.";
            return null;
        }

        if (!FieldRules.IsDate(reviewDate))
        {
            _error = "Review date must be yyyy-MM-dd.";
            return null;
        }

        if (await companies.GetByIdAsync(form.CompanyId, cancellationToken) is null)
        {
            _error = "Company not found.";
            return null;
        }

        return new Prepared(form.CompanyId, role, form.Rating, experience, form.WasLegit, reviewDate);
    }

    private sealed record Prepared(
        int CompanyId,
        string Role,
        int Rating,
        string Experience,
        bool WasLegit,
        string ReviewDate);
}
