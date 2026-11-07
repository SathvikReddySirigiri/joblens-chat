using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

public sealed class DataCheckService(
    IScamQueryRepository queries,
    RecruiterRiskService risk,
    IRdapClient rdap,
    IDatabaseAdmin database)
{
    private static readonly string[] RiskEmails =
    [
        "hr@amaz0n-careers.net",
        "talent@rnicrosoft-hiring.com",
        "recruiting@google.com",
        "deloitte.jobs2026@gmail.com",
        "hr@safe-company-example.com",
    ];

    public string DatabasePath => database.ResolvePath();

    public async Task<(
        DataCounts Counts,
        IReadOnlyList<CompanyRanking> TopCompanies,
        ScamSummary Summary,
        IReadOnlyList<ScamReport> AmazonLookalikeReports,
        IReadOnlyList<RecruiterRiskResult> RiskChecks,
        IReadOnlyList<RdapResult> RdapLookups)> BuildAsync(CancellationToken cancellationToken)
    {
        var risks = new List<RecruiterRiskResult>(RiskEmails.Length);
        foreach (var email in RiskEmails)
        {
            risks.Add(await risk.CheckAsync(email, cancellationToken));
        }

        return (
            await queries.GetCountsAsync(cancellationToken),
            await queries.TopImpersonatedCompaniesAsync(5, null, cancellationToken),
            await queries.SummarizeAsync(null, null, cancellationToken),
            await queries.GetReportsByDomainAsync("amaz0n-careers.net", cancellationToken),
            risks,
            [
                await rdap.LookupAsync("google.com", cancellationToken),
                await rdap.LookupAsync("amaz0n-careers.net", cancellationToken),
            ]);
    }

    public Task ResetAsync(CancellationToken cancellationToken) => database.ResetAsync(cancellationToken);
}
