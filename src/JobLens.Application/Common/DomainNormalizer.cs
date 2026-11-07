using System.Globalization;
using System.Text.RegularExpressions;

namespace JobLens.Application.Common;

public static partial class DomainNormalizer
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

    /// <summary>
    /// MCP-style exact hostname check: optional email username strip, lowercase host, max 100 chars.
    /// </summary>
    public static bool TryNormalizeDomain(string? domain, out string host, out string error)
    {
        host = "";
        var text = domain?.Trim() ?? "";
        if (text.Length == 0)
        {
            error = "Domain is required.";
            return false;
        }

        var at = text.LastIndexOf('@');
        if (at >= 0)
        {
            text = at < text.Length - 1 ? text[(at + 1)..] : "";
        }

        host = text.Trim().ToLowerInvariant();
        if (host.Length == 0)
        {
            error = "Domain is required.";
            return false;
        }

        if (host.Length > 100)
        {
            error = "Domain must be 100 characters or fewer.";
            return false;
        }

        if (!HostnamePattern().IsMatch(host))
        {
            error = "Domain must be an exact hostname such as amaz0n-careers.net, without http://.";
            return false;
        }

        error = "";
        return true;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[a-z0-9.-]+\.[a-z]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex HostnamePattern();
}

// Keep the previous name so existing call sites compile during the move.
public static class FieldRules
{
    public static string Today() => DomainNormalizer.Today();

    public static bool IsEmail(string value) => DomainNormalizer.IsEmail(value);

    public static bool IsDate(string value) => DomainNormalizer.IsDate(value);

    public static string DomainOfEmail(string email) => DomainNormalizer.DomainOfEmail(email);

    public static string NormalizeDomain(string value) => DomainNormalizer.NormalizeDomain(value);
}
