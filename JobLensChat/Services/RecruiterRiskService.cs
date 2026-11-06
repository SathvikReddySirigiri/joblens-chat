using JobLensChat.Data;
using JobLensChat.Models;

namespace JobLensChat.Services;

/// <summary>
/// Deterministic recruiter-email risk score. No model calls; every point comes from a fixed rule.
/// </summary>
public sealed class RecruiterRiskService(ScamRepository repository)
{
    private static readonly HashSet<string> FreeProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com",
        "yahoo.com",
        "outlook.com",
        "hotmail.com",
        "aol.com",
        "icloud.com",
        "proton.me",
        "protonmail.com",
    };

    public async Task<RecruiterRiskResult> CheckAsync(string email, CancellationToken cancellationToken)
    {
        var trimmed = email.Trim();
        var domain = DomainOf(trimmed);
        var companies = await repository.GetCompaniesAsync(cancellationToken);
        var approved = await repository.CountApprovedReportsForDomainAsync(domain, cancellationToken);
        var reports = await repository.GetReportsByDomainAsync(domain, cancellationToken);

        var reasons = new List<string>();
        var score = 10;
        string? matchedCompany = null;
        string? officialDomain = null;
        var matchedRule = false;

        var official = companies.FirstOrDefault(company => IsOfficial(domain, company.OfficialDomain));
        if (official is not null)
        {
            score -= 40;
            matchedCompany = official.Name;
            officialDomain = official.OfficialDomain;
            reasons.Add($"Matches the official domain of {official.Name} ({official.OfficialDomain})");
            matchedRule = true;
        }

        if (FreeProviders.Contains(domain))
        {
            score += 30;
            reasons.Add($"Free email provider ({domain}).");
            matchedRule = true;
        }

        if (official is null)
        {
            var lookalike = FindLookalike(domain, companies);
            if (lookalike is not null)
            {
                score += 40;
                matchedCompany = lookalike.Company.Name;
                officialDomain = lookalike.Company.OfficialDomain;
                reasons.Add($"Look-alike of {lookalike.Company.OfficialDomain}");
                matchedRule = true;

                if (lookalike.UsesCharacterTricks)
                {
                    score += 10;
                    reasons.Add("Character substitutions in the domain (0, 1, 3, 5, rn, or vv).");
                }
            }
        }

        if (approved > 0)
        {
            var points = Math.Min(approved, 3) * 10;
            score += points;
            reasons.Add($"{approved} approved scam report(s) for this domain");
            matchedRule = true;
        }

        if (reports.Count > 0)
        {
            var youngest = reports.Min(report => report.DomainAgeDays);
            if (youngest < 30)
            {
                score += 20;
                reasons.Add($"Domain was only {youngest} days old when reported");
                matchedRule = true;
            }
        }

        if (!matchedRule)
        {
            score += 10;
            reasons.Add("Unknown domain — verify on the company's official careers page");
        }

        score = Math.Clamp(score, 0, 100);
        var verdict = score <= 34 ? "LOW" : score <= 64 ? "MEDIUM" : "HIGH";

        return new RecruiterRiskResult(
            trimmed,
            domain,
            score,
            verdict,
            matchedCompany,
            officialDomain,
            approved,
            reasons);
    }

    private static Lookalike? FindLookalike(string domain, IReadOnlyList<Company> companies)
    {
        var folded = FoldConfusables(domain);
        var normalized = StripPunctuation(folded);
        var firstToken = FirstToken(folded);
        var tricks = !string.Equals(folded, domain, StringComparison.Ordinal);

        Company? best = null;
        var bestRank = int.MaxValue;
        var bestBrandLength = -1;

        foreach (var company in companies)
        {
            var brand = Brand(company.OfficialDomain);
            if (brand.Length == 0)
            {
                continue;
            }

            var contained = normalized.Contains(brand, StringComparison.Ordinal);
            var distance = Levenshtein(firstToken, brand);
            if (!contained && distance > 2)
            {
                continue;
            }

            // A contained brand outranks a mere edit-distance hit. Longer brands win ties.
            var rank = contained ? 0 : distance;
            if (best is null || rank < bestRank || (rank == bestRank && brand.Length > bestBrandLength))
            {
                best = company;
                bestRank = rank;
                bestBrandLength = brand.Length;
            }
        }

        return best is null ? null : new Lookalike(best, tricks);
    }

    private static string DomainOf(string email)
    {
        var at = email.LastIndexOf('@');
        var domain = at >= 0 && at < email.Length - 1 ? email[(at + 1)..] : email;
        return domain.Trim().ToLowerInvariant();
    }

    private static bool IsOfficial(string domain, string official)
    {
        var host = official.Trim().ToLowerInvariant();
        return domain.Equals(host, StringComparison.Ordinal)
            || domain.EndsWith("." + host, StringComparison.Ordinal);
    }

    private static string Brand(string officialDomain)
    {
        var host = officialDomain.Trim().ToLowerInvariant();
        var dot = host.IndexOf('.');
        return dot > 0 ? host[..dot] : host;
    }

    // 0/1/3/5 and the two-letter swaps are the tricks. Punctuation is removed afterwards for the contains check.
    private static string FoldConfusables(string domain)
    {
        return domain
            .Replace("0", "o", StringComparison.Ordinal)
            .Replace("1", "l", StringComparison.Ordinal)
            .Replace("3", "e", StringComparison.Ordinal)
            .Replace("5", "s", StringComparison.Ordinal)
            .Replace("rn", "m", StringComparison.Ordinal)
            .Replace("vv", "w", StringComparison.Ordinal);
    }

    private static string StripPunctuation(string value) => value.Replace("-", "", StringComparison.Ordinal).Replace(".", "", StringComparison.Ordinal);

    // Leftmost token split on '.' and '-'. "amaz0n-careers.net" folds to "amazoon", distance 1 from "amazon".
    // The full hyphenated label is too long to fall within distance 2 of the brand.
    private static string FirstToken(string foldedDomain)
    {
        var token = foldedDomain;
        var cut = token.IndexOfAny(['.', '-']);
        return cut >= 0 ? token[..cut] : token;
    }

    // Wagner–Fischer edit distance for short labels. One row of costs is enough.
    private static int Levenshtein(string source, string target)
    {
        if (source.Length == 0)
        {
            return target.Length;
        }

        if (target.Length == 0)
        {
            return source.Length;
        }

        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];
        for (var column = 0; column <= target.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= source.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= target.Length; column++)
            {
                var substitution = source[row - 1] == target[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    private sealed record Lookalike(Company Company, bool UsesCharacterTricks);
}
