using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Conduit.Domain.Articles;

public static class SlugGenerator
{
    public const int MaxLength = 80;
    private static readonly Regex NonAlnum = new("[^a-z0-9]+", RegexOptions.Compiled);

    public static bool IsReserved(string slug) => slug == "feed";

    public static string Slugify(string title)
    {
        var sb = new StringBuilder();
        foreach (var ch in title.Normalize(NormalizationForm.FormKD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        var s = NonAlnum.Replace(sb.ToString(), "-").Trim('-');
        if (s.Length > MaxLength) s = s[..MaxLength].Trim('-');
        return s.Length == 0 ? "article" : s;
    }

    /// <summary>baseSlug-n, shortened so the whole slug stays within MaxLength.</summary>
    public static string WithSuffix(string baseSlug, int n)
    {
        var suffix = "-" + n.ToString(CultureInfo.InvariantCulture);
        var b = baseSlug.Length + suffix.Length > MaxLength
            ? baseSlug[..(MaxLength - suffix.Length)].TrimEnd('-')
            : baseSlug;
        return b + suffix;
    }
}
