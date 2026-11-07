using JobLens.Application.Common;
using JobLens.Application.Interfaces;
using System.Text.RegularExpressions;

namespace JobLens.Application.Services;

// Strips identifiers before any text is sent to a model or written to a log.
public sealed partial class PiiRedactor
{
    public const int MaxChars = 12_000;

    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var value = CardPattern().Replace(text, "***");
        value = SsnDashedPattern().Replace(value, "***");
        value = PhonePattern().Replace(value, "***");
        value = AccountPattern().Replace(value, "***");
        value = EmailPattern().Replace(value, match =>
        {
            var local = match.Groups[1].Value;
            var first = local.Length == 0 ? "*" : local[..1];
            return first + "***@" + match.Groups[2].Value;
        });

        return value.Length <= MaxChars ? value : value[..MaxChars];
    }

    [GeneratedRegex(@"\b(?:\d{4}[ -]){3}\d{4}\b|\b\d{13,19}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CardPattern();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex SsnDashedPattern();

    [GeneratedRegex(@"(?:\+?1[\s.\-]?)?(?:\(\d{3}\)|\d{3})[\s.\-]\d{3}[\s.\-]\d{4}", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b\d{8,17}\b", RegexOptions.CultureInvariant)]
    private static partial Regex AccountPattern();

    [GeneratedRegex(@"\b([A-Za-z0-9._%+\-]+)@([A-Za-z0-9.-]+\.[A-Za-z]{2,})\b", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
