namespace RpCalculator.Core;

/// <summary>
/// 识别码格式的规范化与验证。
///
/// 固定格式：16 位十六进制字符（大写 0-9 / A-F），按 4-4-4-4 分组、段间用 '-' 连接，
/// 形如 "7123-4567-890A-BCDE"。
///
/// 无效规则（与另一个应用的一致性约定）：
/// - 去掉短横线后以 '0' 开头的识别码，会被另一个应用规范化成不同的识别码，
///   对当前计算而言属于无效输入，必须排除。
/// - 长度不为 16、或包含非十六进制字符、或第一个字符为 '0' 的识别码均为无效。
/// </summary>
public static class IdFormat
{
    /// <summary>不带短横线的原始十六进制长度。</summary>
    public const int HexLength = 16;

    /// <summary>分组长度（4-4-4-4）。</summary>
    private const int GroupLength = 4;

    /// <summary>段数。</summary>
    private const int GroupCount = HexLength / GroupLength;

    /// <summary>首字符候选集：排除 '0'，保证规范化后不被另一个应用改写。</summary>
    public const string FirstHexChars = "123456789ABCDEF";

    /// <summary>全部十六进制字符（大写）。</summary>
    public const string HexChars = "0123456789ABCDEF";

    private static readonly char[] HexLookup = HexChars.ToCharArray();

    /// <summary>
    /// 验证并规范化一个原始输入（文件导入场景）：
    /// 去除首尾空格 → 移除所有 '-' → 转大写 → 校验 16 位十六进制且首字符非 '0'，
    /// 成功时输出带短横线的标准格式。
    /// </summary>
    /// <param name="raw">文件行原始输入，可为 null 或空白。</param>
    /// <param name="id">规范化后的标准识别码（带短横线、大写）。</param>
    /// <returns>true 表示有效并输出了 id；false 表示无效（跳过）。</returns>
    public static bool TryNormalize(string? raw, out string id)
    {
        id = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // 1. 去除首尾空格；2. 移除所有 '-'；3. 统一大写。
        var hex = raw.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

        // 4. 长度必须为 16，且全部为十六进制字符。
        if (hex.Length != HexLength || !IsHexOnly(hex))
        {
            return false;
        }

        // 5. 第一个字符不能是 '0'（去掉横线后的首字符）。
        if (hex[0] == '0')
        {
            return false;
        }

        id = Format(hex);
        return true;
    }

    /// <summary>判断一个已经规范化的识别码（带短横线）是否有效。</summary>
    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var hex = id.Replace("-", string.Empty, StringComparison.Ordinal);
        return hex.Length == HexLength
               && hex[0] != '0'
               && IsHexOnly(hex);
    }

    /// <summary>
    /// 把 16 位纯十六进制格式化为 4-4-4-4 带短横线格式。
    /// 调用方需保证输入是 16 位十六进制。
    /// </summary>
    public static string Format(string hex16)
    {
        var buffer = new char[HexLength + GroupCount - 1]; // 16 + 3 = 19
        var pos = 0;

        for (var group = 0; group < GroupCount; group++)
        {
            if (group > 0)
            {
                buffer[pos++] = '-';
            }

            for (var j = 0; j < GroupLength; j++)
            {
                buffer[pos++] = hex16[group * GroupLength + j];
            }
        }

        return new string(buffer, 0, pos);
    }

    private static bool IsHexOnly(string s)
    {
        foreach (var c in s)
        {
            if ((c < '0' || c > '9') && (c < 'A' || c > 'F'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>把 0-15 的 nibble 映射为大写十六进制字符。</summary>
    internal static char ToHexChar(int nibble)
    {
        return HexLookup[nibble & 0xF];
    }
}
