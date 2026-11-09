using System.Text;

namespace JobLens.Application.Common;

public static class CsvExport
{
    public static string Escape(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && "=+-@".Contains(text[0]))
        {
            text = "'" + text;
        }

        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        return text;
    }

    public static string Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(Escape)));
        }

        return builder.ToString();
    }
}
