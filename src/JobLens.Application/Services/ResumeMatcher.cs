using JobLens.Application.Dtos;
using JobLens.Domain.Skills;

namespace JobLens.Application.Services;

public static class ResumeMatcher
{
    /// <summary>
    /// Score = matched / required skills found in the job text (catalog-based).
    /// When the job text has no catalog skills, score is 100 (nothing missing).
    /// </summary>
    public static ResumeMatchResult Match(string? jobText, IEnumerable<string>? resumeSkills)
    {
        var required = SkillCatalog.Extract(jobText);
        var resume = new HashSet<string>(
            (resumeSkills ?? []).Where(skill => !string.IsNullOrWhiteSpace(skill)).Select(skill => skill.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var matched = required.Where(skill => resume.Contains(skill)).ToList();
        var missing = required.Where(skill => !resume.Contains(skill)).ToList();
        var score = required.Count == 0
            ? 100
            : (int)Math.Round(matched.Count * 100.0 / required.Count, MidpointRounding.AwayFromZero);

        return new ResumeMatchResult(score, required, matched, missing);
    }

    public static IReadOnlyList<string> ParseSkillsCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(skill => skill.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    public static string ToSkillsCsv(IEnumerable<string> skills) =>
        string.Join(",", skills.Distinct(StringComparer.OrdinalIgnoreCase));
}
