using JobLens.Application.Common;
using JobLens.Application.Dtos;
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
        CancellationToken cancellationToken)
    {
        var result = await risk.CheckAsync(email, claimedCompanyName, cancellationToken);
        await logs.AddAsync(
            new CheckLog(0, "Recruiter", result.Domain, result.Score, result.Verdict, FieldRules.Today()),
            cancellationToken);
        var related = await reports.BrowseAsync(result.Domain, "Approved", null, null, null, "date", true, 1, cancellationToken);
        var recruiter = await recruiters.GetByEmailAsync(result.Email, cancellationToken);
        return (result, related.Items.Take(5).ToList(), recruiter);
    }
}
