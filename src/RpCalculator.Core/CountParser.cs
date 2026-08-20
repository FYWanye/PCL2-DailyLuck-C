using System.Globalization;

namespace RpCalculator.Core;

/// <summary>
/// 识别码数量解析，支持普通整数与科学计数法，如 "10000000000"、"1e10"、"1.5e6"。
/// </summary>
public static class CountParser
{
    public static bool TryParse(string? text, out long count)
    {
        count = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().Replace(",", string.Empty, StringComparison.Ordinal);

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
        {
            return count >= 0;
        }

        if (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            if (value < 0 || value != decimal.Truncate(value) || value > long.MaxValue)
            {
                return false;
            }

            count = (long)value;
            return true;
        }

        return false;
    }
}
