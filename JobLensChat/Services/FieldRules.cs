using System.Globalization;
using System.Text.RegularExpressions;

namespace JobLensChat.Services;

internal static partial class FieldRules
{
    public static string Today() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool IsEmail(string value) => EmailPattern().IsMatch(value);

    public static bool IsDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static string DomainOfEmail(string email)
    {
        var at = email.LastIndexOf('@');
        return at < 0 || at >= email.Length - 1 ? "" : email[(at + 1)..].Trim().ToLowerInvariant();
    }

    public static string NormalizeDomain(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        if (text.StartsWith("https://", StringComparison.Ordinal))
        {
            text = text[8..];
        }
        else if (text.StartsWith("http://", StringComparison.Ordinal))
        {
            text = text[7..];
        }

        var slash = text.IndexOf('/');
        if (slash >= 0)
        {
            text = text[..slash];
        }

        if (text.StartsWith("www.", StringComparison.Ordinal))
        {
            text = text[4..];
        }

        return text.Trim().TrimEnd('.');
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
