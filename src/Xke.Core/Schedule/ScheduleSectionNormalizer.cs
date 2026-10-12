using System.Net;
using System.Text.RegularExpressions;

namespace Xke.Core.Schedule;

internal static class ScheduleSectionNormalizer
{
    private const string RangeSeparatorPattern = @"[-~～—－–至到]";
    private static readonly Regex HtmlTagPattern = new(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex ClockRangePattern = new($@"\d{{1,2}}:\d{{2}}\s*{RangeSeparatorPattern}\s*\d{{1,2}}:\d{{2}}", RegexOptions.Compiled);
    private static readonly Regex WeekSpanPattern = new($@"(?:第\s*)?\d{{1,2}}\s*(?:{RangeSeparatorPattern}\s*\d{{1,2}})?(?:\s*[,，、]\s*\d{{1,2}})*\s*周", RegexOptions.Compiled);
    private static readonly Regex WeekQualifierPattern = new(@"[单双]周|每周", RegexOptions.Compiled);
    private static readonly Regex WeekdayPattern = new(@"星期[一二三四五六日天]|周[一二三四五六日天]", RegexOptions.Compiled);
    private static readonly Regex CompactSectionRangeWithSuffixPattern = new($@"(?<!\d)(\d{{1,2}})\s*{RangeSeparatorPattern}\s*(\d{{4,}})\s*(?:小?节)(?!\d)", RegexOptions.Compiled);
    private static readonly Regex SectionHyphenListWithSuffixPattern = new(@"(?<!\d)((?:\d{1,2}\s*-\s*){2,}\d{1,2})\s*(?:小?节)(?!\d)", RegexOptions.Compiled);
    private static readonly Regex SectionRangeWithSuffixPattern = new($@"(?:第\s*)?(\d{{1,2}})\s*{RangeSeparatorPattern}\s*(\d{{1,2}})\s*(?:小?节)", RegexOptions.Compiled);
    private static readonly Regex SectionListWithSuffixPattern = new(@"(?<!\d)((?:\d{1,2}\s*[,，、]\s*)+\d{1,2})\s*(?:小?节)(?!\d)", RegexOptions.Compiled);
    private static readonly Regex CompactSectionWithSuffixPattern = new(@"(?<!\d)(\d{4,})\s*(?:小?节)(?!\d)", RegexOptions.Compiled);
    private static readonly Regex SectionSingleWithSuffixPattern = new(@"(?:第\s*)?(\d{1,2})\s*(?:小?节)", RegexOptions.Compiled);
    private static readonly Regex LooseRangePattern = new($@"(?<!\d)(\d{{1,2}})\s*{RangeSeparatorPattern}\s*(\d{{1,2}})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex NumberTokenPattern = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex BareNumberListPattern = new(@"^(?:第\s*)?\d{1,2}(?:\s*[,，、/／|+]\s*\d{1,2})*$", RegexOptions.Compiled);
    private static readonly Regex ContextSeparatorPattern = new(@"[/／|,，、;；:：()\[\]{}（）\s]+", RegexOptions.Compiled);

    public static string NormalizeSectionText(string? value)
    {
        var numbers = ExtractSectionNumbers(value);
        if (numbers.Count > 0)
        {
            return FormatSectionRanges(numbers);
        }

        return LooksLikeScheduleContextOnly(value) ? string.Empty : NormalizeInput(value);
    }

    public static List<int> ExtractSectionNumbers(string? value)
    {
        var text = RemoveScheduleContext(NormalizeInput(value));
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var hyphenList = new List<int>();
        AddListMatches(hyphenList, SectionHyphenListWithSuffixPattern.Matches(text));
        if (hyphenList.Count > 0) return SortSections(hyphenList);
        var suffixed = new List<int>();
        AddCompactRangeMatches(suffixed, CompactSectionRangeWithSuffixPattern.Matches(text));
        AddRangeMatches(suffixed, SectionRangeWithSuffixPattern.Matches(text));
        AddListMatches(suffixed, SectionListWithSuffixPattern.Matches(text));
        AddCompactMatches(suffixed, CompactSectionWithSuffixPattern.Matches(text));
        foreach (Match match in SectionSingleWithSuffixPattern.Matches(text))
        {
            var number = int.Parse(match.Groups[1].Value);
            if (IsValidSectionNumber(number))
            {
                suffixed.Add(number);
            }
        }

        if (suffixed.Count > 0)
        {
            return SortSections(suffixed);
        }

        var looseRanges = new List<int>();
        AddRangeMatches(looseRanges, LooseRangePattern.Matches(text));
        if (looseRanges.Count > 0)
        {
            return SortSections(looseRanges);
        }

        var numbers = new List<int>();
        var allowBareNumbers = AllowsBareNumberTokens(text);
        foreach (Match tokenMatch in NumberTokenPattern.Matches(text))
        {
            var token = tokenMatch.Value;
            var pairs = SplitCompactSections(token);
            if (pairs.Count > 0)
            {
                numbers.AddRange(pairs);
                continue;
            }

            if (!allowBareNumbers)
            {
                continue;
            }

            var number = int.Parse(token);
            if (IsValidSectionNumber(number))
            {
                numbers.Add(number);
            }
        }

        return SortSections(numbers);
    }

    public static string FormatSectionRanges(IReadOnlyList<int> numbers)
    {
        var sorted = SortSections(numbers);
        if (sorted.Count == 0)
        {
            return string.Empty;
        }

        var ranges = new List<string>();
        var start = sorted[0];
        var previous = sorted[0];
        foreach (var number in sorted.Skip(1))
        {
            if (number == previous + 1)
            {
                previous = number;
                continue;
            }

            ranges.Add(start == previous ? start.ToString() : $"{start}-{previous}");
            start = previous = number;
        }

        ranges.Add(start == previous ? start.ToString() : $"{start}-{previous}");
        return $"第{string.Join(",", ranges)}节";
    }

    public static bool LooksLikeSectionText(string? value)
    {
        return ExtractSectionNumbers(value).Count > 0;
    }

    public static bool LooksLikeScheduleContextOnly(string? value)
    {
        var original = NormalizeInput(value);
        if (string.IsNullOrWhiteSpace(original) || ExtractSectionNumbers(original).Count > 0)
        {
            return false;
        }

        if (!original.Contains("周", StringComparison.Ordinal)
            && !WeekdayPattern.IsMatch(original)
            && !ClockRangePattern.IsMatch(original))
        {
            return false;
        }

        var withoutContext = RemoveScheduleContext(original);
        withoutContext = ContextSeparatorPattern.Replace(withoutContext, string.Empty);
        return string.IsNullOrWhiteSpace(withoutContext);
    }

    private static void AddRangeMatches(List<int> target, MatchCollection matches)
    {
        foreach (Match match in matches)
        {
            var start = int.Parse(match.Groups[1].Value);
            var end = int.Parse(match.Groups[2].Value);
            if (!IsValidSectionNumber(start) || !IsValidSectionNumber(end))
            {
                continue;
            }

            if (start <= end)
            {
                target.AddRange(Enumerable.Range(start, end - start + 1));
            }
            else
            {
                target.Add(start);
                target.Add(end);
            }
        }
    }

    private static void AddCompactRangeMatches(List<int> target, MatchCollection matches)
    {
        foreach (Match match in matches)
        {
            var start = int.Parse(match.Groups[1].Value);
            if (IsValidSectionNumber(start))
            {
                target.Add(start);
            }

            target.AddRange(SplitCompactSections(match.Groups[2].Value));
        }
    }

    private static void AddCompactMatches(List<int> target, MatchCollection matches)
    {
        foreach (Match match in matches)
        {
            target.AddRange(SplitCompactSections(match.Groups[1].Value));
        }
    }

    private static void AddListMatches(List<int> target, MatchCollection matches)
    {
        foreach (Match match in matches)
        {
            foreach (Match numberMatch in NumberTokenPattern.Matches(match.Groups[1].Value))
            {
                var number = int.Parse(numberMatch.Value);
                if (IsValidSectionNumber(number))
                {
                    target.Add(number);
                }
            }
        }
    }

    private static List<int> SplitCompactSections(string token)
    {
        if (token.Length >= 4 && token.Length % 2 == 0)
        {
            var pairs = SplitEvenCompactSections(token);
            if (pairs.Count > 0)
            {
                return pairs;
            }
        }

        if (token.Length >= 5 && token.Length % 2 == 1)
        {
            return SplitEvenCompactSections(token[1..]);
        }

        return [];
    }

    private static List<int> SplitEvenCompactSections(string token)
    {
        if (token.Length % 2 != 0)
        {
            return [];
        }

        var pairs = Enumerable.Range(0, token.Length / 2)
            .Select(index => int.Parse(token.Substring(index * 2, 2)))
            .ToList();
        return pairs.All(IsValidSectionNumber) ? pairs : [];
    }

    private static bool AllowsBareNumberTokens(string text)
    {
        return BareNumberListPattern.IsMatch(text);
    }

    private static string RemoveScheduleContext(string text)
    {
        var cleaned = ClockRangePattern.Replace(text, " ");
        cleaned = WeekSpanPattern.Replace(cleaned, " ");
        cleaned = WeekQualifierPattern.Replace(cleaned, " ");
        cleaned = WeekdayPattern.Replace(cleaned, " ");
        return NormalizeSpaces(cleaned);
    }

    private static string NormalizeInput(string? value)
    {
        var decoded = WebUtility.HtmlDecode(value ?? string.Empty);
        var withoutTags = HtmlTagPattern.Replace(decoded, " ");
        return NormalizeSpaces(withoutTags);
    }

    private static string NormalizeSpaces(string value)
    {
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static List<int> SortSections(IEnumerable<int> numbers)
    {
        return numbers.Where(IsValidSectionNumber).Distinct().Order().ToList();
    }

    private static bool IsValidSectionNumber(int value)
    {
        return value is >= 1 and <= 20;
    }
}
