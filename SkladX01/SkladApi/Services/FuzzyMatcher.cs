namespace SkladApi.Services;

/// <summary>
/// Сравнение строк по расстоянию Левенштейна.
/// Нужно, чтобы сопоставить продиктованное название с номенклатурой без ИИ.
/// </summary>
public static class FuzzyMatcher
{
    private static readonly System.Text.RegularExpressions.Regex NumbersRegex =
        new(@"\d+", System.Text.RegularExpressions.RegexOptions.Compiled);
    /// <summary>Схожесть двух строк в процентах: 0 — ничего общего, 100 — совпадение.</summary>
    public static int Similarity(string? source, string? target)
    {
        var left = Normalize(source);
        var right = Normalize(target);

        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        if (left == right)
        {
            return 100;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;

                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        var distance = previous[right.Length];
        var maxLength = Math.Max(left.Length, right.Length);
        var similarity = (1.0 - (double)distance / maxLength) * 100;

        return (int)Math.Round(similarity);
    }

    /// <summary>Нижний регистр, «ё» → «е», лишние пробелы и знаки убраны.</summary>
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .ToLowerInvariant()
            .Replace('ё', 'е')
            .Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ')
            .ToArray();

        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// true, если в обеих строках есть числа и ни одно не совпадает.
    /// Так «гвозди 2,5х50» не притягивается к «Гвозди 50» только по слову.
    /// </summary>
    public static bool NumbersConflict(string? source, string? target)
    {
        var left = ExtractNumbers(source);
        var right = ExtractNumbers(target);

        return left.Count > 0 && right.Count > 0 && !left.Overlaps(right);
    }

    private static HashSet<string> ExtractNumbers(string? value)
    {
        var numbers = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(value))
        {
            return numbers;
        }

        // «4.2х75» должно дать 4, 2 и 75, а не «4275».
        foreach (System.Text.RegularExpressions.Match match in NumbersRegex.Matches(value))
        {
            numbers.Add(match.Value);
        }

        return numbers;
    }
}
