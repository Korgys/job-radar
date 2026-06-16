using System.Globalization;
using System.Text;

namespace JobRadarLocal.Services;

public static class RadarText
{
    public const int MaxStackItemLength = 30;

    public static IReadOnlyList<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim().Trim('"'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string JoinList(IEnumerable<string> values)
    {
        return string.Join(';', values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public static string NormalizeSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool ContainsNormalized(string text, string candidate)
    {
        return NormalizeSearch(text).Contains(NormalizeSearch(candidate), StringComparison.Ordinal);
    }

    public static string CleanList(string? value)
    {
        return JoinList(SplitList(value));
    }

    public static string CleanStackList(string? value)
    {
        return JoinList(SplitList(value).Select(item => item.Length > MaxStackItemLength ? item[..MaxStackItemLength] : item));
    }
}
