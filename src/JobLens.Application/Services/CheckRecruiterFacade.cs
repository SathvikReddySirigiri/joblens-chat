using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Dtos.Pages;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

/// <summary>UI orchestration for Check Recruiter: score, log, related reports.</summary>
public sealed class CheckRecruiterFacade(
    RecruiterRiskService risk,
    CompanyService companies,
    ScamReportService reports,
    RecruiterService recruiters,
    ICheckLogRepository logs)
{
    public Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken cancellationToken) =>
        companies.GetAllAsync(cancellationToken);

    public async Task<(RecruiterRiskResult Result, IReadOnlyList<ScamReport> Reports, Recruiter? Recruiter)> CheckAsync(
        string email,
        string? claimedCompanyName,
        int? userId,
        CancellationToken cancellationToken)
    {
        var result = await risk.CheckAsync(email, claimedCompanyName, cancellationToken);
        await logs.AddAsync(
            new CheckLog(0, "Recruiter", result.Domain, result.Score, result.Verdict, FieldRules.Today(), userId),
            cancellationToken);
        var related = await reports.GetApprovedByExactDomainAsync(result.Domain, 5, cancellationToken);
        var recruiter = await recruiters.GetByEmailAsync(result.Email, cancellationToken);
        return (result, related, recruiter);
    }

    public async Task<IReadOnlyList<RecentCheckItem>> RecentForUserAsync(int userId, CancellationToken cancellationToken)
    {
        var rows = await logs.RecentForUserAsync(userId, 5, cancellationToken);
        return rows
            .Select(row => new RecentCheckItem(row.CheckId, row.InputSummary, row.Verdict, row.CreatedAt))
            .ToList();
    }
}
