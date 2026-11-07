using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

public sealed class InterviewReviewService(IInterviewReviewRepository reviews, ICompanyRepository companies)
{
    public async Task<ServiceResult> CreateAsync(InterviewReviewFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        if (actor.UserId <= 0)
        {
            return ServiceResult.Fail("You must be signed in to create a review.");
        }

        var (prepared, error) = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(error);
        }

        var id = await reviews.CreateAsync(
            prepared.CompanyId,
            prepared.Role,
            prepared.Rating,
            prepared.Experience,
            prepared.WasLegit,
            prepared.ReviewDate,
            actor.UserId,
            cancellationToken);
        return ServiceResult.Ok(id);
    }

    public async Task<ServiceResult> UpdateAsync(InterviewReviewFormVm form, ActingUser actor, CancellationToken cancellationToken)
    {
        var existing = await reviews.GetByIdAsync(form.ReviewId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Review not found.");
        }

        if (!CanEdit(existing, actor))
        {
            return ServiceResult.Fail("You can only edit your own interview reviews.");
        }

        var (prepared, error) = await TryPrepareAsync(form, cancellationToken);
        if (prepared is null)
        {
            return ServiceResult.Fail(error);
        }

        var updated = await reviews.UpdateAsync(
            form.ReviewId,
            prepared.CompanyId,
            prepared.Role,
            prepared.Rating,
            prepared.Experience,
            prepared.WasLegit,
            prepared.ReviewDate,
            cancellationToken);
        return updated ? ServiceResult.Ok(form.ReviewId) : ServiceResult.Fail("Review not found.");
    }

    public async Task<ServiceResult> DeleteAsync(int reviewId, ActingUser actor, CancellationToken cancellationToken)
    {
        var existing = await reviews.GetByIdAsync(reviewId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult.Fail("Review not found.");
        }

        if (!CanDelete(existing, actor))
        {
            return ServiceResult.Fail("You can only delete your own interview reviews.");
        }

        var deleted = await reviews.DeleteAsync(reviewId, cancellationToken);
        return deleted ? ServiceResult.Ok(reviewId) : ServiceResult.Fail("Review not found.");
    }

    public bool CanEdit(InterviewReview review, ActingUser? actor)
    {
        if (actor is null)
        {
            return false;
        }

        return actor.IsModerator || (actor.IsStudent && review.CreatedByUserId == actor.UserId);
    }

    public bool CanDelete(InterviewReview review, ActingUser? actor) => CanEdit(review, actor);

    private async Task<(Prepared? Prepared, string Error)> TryPrepareAsync(
        InterviewReviewFormVm form,
        CancellationToken cancellationToken)
    {
        var role = form.Role?.Trim() ?? "";
        var experience = form.Experience?.Trim() ?? "";
        var reviewDate = string.IsNullOrWhiteSpace(form.ReviewDate) ? FieldRules.Today() : form.ReviewDate.Trim();
        if (role.Length is 0 or > 120)
        {
            return (null, "Role is required.");
        }

        if (form.Rating is < 1 or > 5)
        {
            return (null, "Rating must be from 1 to 5.");
        }

        if (experience.Length is < 20 or > 1000)
        {
            return (null, "Experience must be 20 to 1000 characters.");
        }

        if (!FieldRules.IsDate(reviewDate))
        {
            return (null, "Review date must be yyyy-MM-dd.");
        }

        if (await companies.GetByIdAsync(form.CompanyId, cancellationToken) is null)
        {
            return (null, "Company not found.");
        }

        return (new Prepared(form.CompanyId, role, form.Rating, experience, form.WasLegit, reviewDate), "");
    }

    private sealed record Prepared(
        int CompanyId,
        string Role,
        int Rating,
        string Experience,
        bool WasLegit,
        string ReviewDate);

    public Task<PageResult<InterviewReview>> BrowseAsync(int? companyId, int? rating, bool? wasLegit, int page, CancellationToken cancellationToken) =>
        reviews.BrowseAsync(companyId, rating, wasLegit, page, cancellationToken);

    public Task<InterviewReview?> GetByIdAsync(int reviewId, CancellationToken cancellationToken) =>
        reviews.GetByIdAsync(reviewId, cancellationToken);
}
