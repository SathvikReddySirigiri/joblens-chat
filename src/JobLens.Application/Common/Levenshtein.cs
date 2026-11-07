namespace JobLens.Application.Common;

public static class Levenshtein
{
    public static int Distance(string source, string target)
    {
        var n = source.Length;
        var m = target.Length;
        var previous = new int[m + 1];
        var current = new int[m + 1];
        for (var j = 0; j <= m; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            current[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[m];
    }
}
