namespace Xke.Core.Update;

public static class UpdateVersionComparer
{
    public static int Compare(string? left, string? right)
    {
        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        var count = Math.Max(leftTokens.Length, rightTokens.Length);

        for (var index = 0; index < count; index++)
        {
            var leftToken = index < leftTokens.Length ? leftTokens[index] : "0";
            var rightToken = index < rightTokens.Length ? rightTokens[index] : "0";
            var leftNumeric = long.TryParse(leftToken, out var leftNumber);
            var rightNumeric = long.TryParse(rightToken, out var rightNumber);
            var compared = leftNumeric && rightNumeric
                ? leftNumber.CompareTo(rightNumber)
                : string.Compare(leftToken, rightToken, StringComparison.OrdinalIgnoreCase);
            if (compared != 0)
            {
                return compared < 0 ? -1 : 1;
            }
        }

        return 0;
    }

    private static string[] Tokenize(string? version)
    {
        return (version ?? string.Empty)
            .Trim()
            .TrimStart('v', 'V')
            .Split(['.', '-', '_', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
