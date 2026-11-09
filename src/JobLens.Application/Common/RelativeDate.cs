using System.Globalization;

namespace JobLens.Application.Common;

public static class RelativeDate
{
    public static string Format(string isoDate, string? todayIso = null)
    {
        if (!DateOnly.TryParse(isoDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return isoDate;
        }

        var today = DateOnly.TryParse(todayIso ?? FieldRules.Today(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);
        var days = today.DayNumber - date.DayNumber;
        if (days <= 0)
        {
            return "today";
        }

        if (days == 1)
        {
            return "1 day ago";
        }

        if (days < 30)
        {
            return $"{days} days ago";
        }

        var months = days / 30;
        if (months == 1)
        {
            return "1 month ago";
        }

        if (months < 12)
        {
            return $"{months} months ago";
        }

        var years = months / 12;
        return years == 1 ? "1 year ago" : $"{years} years ago";
    }

    public static string FormatCompactUsd(double amount)
    {
        if (amount < 1000)
        {
            return amount.ToString("$#,##0.##", CultureInfo.InvariantCulture);
        }

        if (amount < 1_000_000)
        {
            var thousands = amount / 1000.0;
            return "$" + thousands.ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }

        var millions = amount / 1_000_000.0;
        return "$" + millions.ToString("0.#", CultureInfo.InvariantCulture) + "M";
    }
}
