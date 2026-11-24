using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PowerMonitor.Core.Schedule;

/// <summary>
/// Helpers for address lookups. Directory entries and typed queries are untrusted: labels lose
/// control and format characters and get a length cap, and ids are digits only before they go
/// into a request.
/// </summary>
public static class AddressText
{
    public const int MaxChoices = 40;
    public const int MaxLabel = 80;
    public const int MaxQuery = 60;

    private static readonly Regex IdPattern = new("^[0-9]{1,12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex NonWord = new(@"[\W_]+", RegexOptions.Compiled);
    private static readonly Regex Digits = new("([0-9]{1,9})", RegexOptions.Compiled);

    // "вул. ", "просп.", "м." ... at the start: users type them or not, directories differ.
    private static readonly Regex Kind = new(
        @"^(?:вулиця|вул|проспект|просп|пр-т|пр|провулок|пров|бульвар|бульв|бул|площа|пл|" +
        @"місто|м|село|с|селище|смт|с-ще|будинок|буд|№)(?:\.\s{0,3}|\s{1,3})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string? NumericId(string? value) =>
        value is not null && IdPattern.IsMatch(value) ? value : null;

    public static string RequireId(string value) =>
        NumericId(value) ?? throw new ArgumentException("Not a directory id.", nameof(value));

    /// <summary>Directory text fit to show: one line, visible characters only, at most <paramref name="limit"/>.</summary>
    public static string? CleanLabel(string? value, int limit = MaxLabel)
    {
        if (value is null)
            return null;

        var text = Collapse(value.Length > limit * 4 ? value[..(limit * 4)] : value);
        if (text.Length > limit)
            text = text[..(limit - 1)].TrimEnd() + "…";
        return text.Length == 0 ? null : text;
    }

    /// <summary>What the user typed, as one line of at most <see cref="MaxQuery"/> visible characters.</summary>
    public static string CleanQuery(string? text)
    {
        var cleaned = Collapse(text is { Length: > MaxQuery * 4 } ? text[..(MaxQuery * 4)] : text ?? "");
        return cleaned.Length > MaxQuery ? cleaned[..MaxQuery] : cleaned;
    }

    /// <summary>"вул. Руська" becomes "Руська".</summary>
    public static string StripKind(string text)
    {
        for (var i = 0; i < 2; i++)
            text = Kind.Replace(text, "", 1);
        return text;
    }

    public static string Fold(string text) =>
        text.Normalize(NormalizationForm.FormKC)
            .ToLowerInvariant()
            .Replace('’', '\'').Replace('ʼ', '\'').Replace('`', '\'')
            .Replace('′', '\'').Replace('‘', '\'').Replace('´', '\'');

    /// <summary>Letters and digits only: "10-А" and "10 а" both become "10а".</summary>
    public static string Compact(string text) => NonWord.Replace(Fold(text), "");

    public static IReadOnlyList<string> Words(string query) =>
        Fold(StripKind(query)).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Compact)
            .Where(w => w.Length > 0)
            .ToList();

    /// <summary>The longest word of a query, for directories that search by one substring.</summary>
    public static string SearchTerm(string query) =>
        Fold(StripKind(query)).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .OrderByDescending(w => w.Length)
            .FirstOrDefault() ?? "";

    /// <summary>Every word of the query appears in the label, ignoring case, punctuation and kind.</summary>
    public static bool Matches(string label, string query)
    {
        var target = Compact(StripKind(label));
        return Words(query).All(target.Contains);
    }

    /// <summary>Choices matching the query: exact first, then by prefix, then the rest; no duplicates.</summary>
    public static IReadOnlyList<AddressChoice> Pick(
        IEnumerable<AddressChoice> choices,
        string query,
        int limit = MaxChoices)
    {
        var unique = choices.GroupBy(c => c.Id).Select(g => g.First()).ToList();
        if (string.IsNullOrWhiteSpace(query))
            return unique.OrderBy(c => StripKind(c.Label), NaturalComparer.Instance).Take(limit).ToList();

        var whole = string.Concat(Words(query));
        int Rank(AddressChoice choice)
        {
            var target = Compact(StripKind(choice.Label));
            return target == whole ? 0 : target.StartsWith(whole, StringComparison.Ordinal) ? 1 : 2;
        }

        return unique
            .Where(c => Matches(c.Label, query))
            .OrderBy(Rank)
            .ThenBy(c => StripKind(c.Label), NaturalComparer.Instance)
            .Take(limit)
            .ToList();
    }

    private static string Collapse(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            switch (Rune.GetUnicodeCategory(rune))
            {
                case UnicodeCategory.Control:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                    builder.Append(' ');
                    break;
                case UnicodeCategory.Format:
                case UnicodeCategory.PrivateUse:
                case UnicodeCategory.Surrogate:
                case UnicodeCategory.OtherNotAssigned:
                    break;
                default:
                    builder.Append(rune.ToString());
                    break;
            }
        }

        return Spaces.Replace(builder.ToString(), " ").Trim();
    }

    /// <summary>Sorts "2" before "10" and "10" before "10-А".</summary>
    private sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var a = Digits.Split(Fold(x ?? "")).Where(p => p.Length > 0).ToArray();
            var b = Digits.Split(Fold(y ?? "")).Where(p => p.Length > 0).ToArray();
            for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                var aNum = a[i].All(char.IsAsciiDigit);
                var bNum = b[i].All(char.IsAsciiDigit);
                int result;
                if (aNum && bNum)
                    result = long.Parse(a[i], CultureInfo.InvariantCulture)
                        .CompareTo(long.Parse(b[i], CultureInfo.InvariantCulture));
                else if (aNum != bNum)
                    result = aNum ? -1 : 1;
                else
                    result = string.CompareOrdinal(a[i], b[i]);
                if (result != 0)
                    return result;
            }

            return a.Length.CompareTo(b.Length);
        }
    }
}
