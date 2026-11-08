using System.Text.RegularExpressions;
using JobLens.Application.Dtos;

namespace JobLens.Application.Services;

public static partial class ResumePrivacyChecker
{
    public const string Advice =
        "Employers don't need this on a resume — scammers collect it. Remove it before sharing.";

    public static IReadOnlyList<PrivacyFinding> Check(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var findings = new List<PrivacyFinding>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            TryAdd(findings, trimmed, SsnPattern(), "Social Security number", "High");
            TryAdd(findings, trimmed, DobPattern(), "Date of birth", "High");
            TryAdd(findings, trimmed, AddressPattern(), "Home address", "High");
            TryAdd(findings, trimmed, PassportPattern(), "Passport / ID number", "High");
            TryAdd(findings, trimmed, DriverLicensePattern(), "Driver license number", "High");
            TryAdd(findings, trimmed, BankPattern(), "Bank / routing number", "High");
            TryAdd(findings, trimmed, AgePattern(), "Age", "Medium");
            TryAdd(findings, trimmed, MaritalPattern(), "Marital status", "Medium");
            TryAdd(findings, trimmed, PhotoPattern(), "Photo mention", "Medium");
        }

        return findings;
    }

    private static void TryAdd(
        List<PrivacyFinding> findings,
        string line,
        Regex pattern,
        string category,
        string severity)
    {
        if (!pattern.IsMatch(line))
        {
            return;
        }

        if (findings.Any(item =>
                item.Category == category
                && item.MaskedLine.Equals(Mask(line, pattern), StringComparison.Ordinal)))
        {
            return;
        }

        findings.Add(new PrivacyFinding(category, severity, Mask(line, pattern), Advice));
    }

    private static string Mask(string line, Regex pattern) =>
        pattern.Replace(line, match => new string('*', Math.Min(match.Length, 12)));

    [GeneratedRegex(@"\b(?:SSN|Social Security)\b[:\s#]*\d{3}-\d{2}-\d{4}\b|\b\d{3}-\d{2}-\d{4}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SsnPattern();

    [GeneratedRegex(@"\b(?:DOB|D\.?O\.?B\.?|Date of Birth)\b[:\s]*\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b|\b(?:born|birthday)\b[:\s]*\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DobPattern();

    [GeneratedRegex(@"\b\d{1,5}\s+[A-Za-z0-9.'\- ]+(?:Street|St\.?|Avenue|Ave\.?|Road|Rd\.?|Boulevard|Blvd\.?|Lane|Ln\.?|Drive|Dr\.?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AddressPattern();

    [GeneratedRegex(@"\b(?:passport)\b[:\s#]*[A-Z0-9]{6,12}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PassportPattern();

    [GeneratedRegex(@"\b(?:driver'?s?\s*license|DL)\b[\s#:]*[A-Z0-9\-]{5,20}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DriverLicensePattern();

    [GeneratedRegex(@"\b(?:routing|bank\s*account|account)\s*(?:number|no\.?|#)?[:\s]*\d{8,17}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BankPattern();

    [GeneratedRegex(@"\bage\b[:\s]*\d{1,2}\b|\b\d{1,2}\s*years old\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AgePattern();

    [GeneratedRegex(@"\bmarital status\b|\b(?:married|divorced|widowed)\b|\bI am single\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MaritalPattern();

    [GeneratedRegex(@"\b(?:photo|photograph|headshot|picture)\s+(?:available|attached|enclosed|included)\b|\b(?:see attached photo|photo upon request)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PhotoPattern();
}
