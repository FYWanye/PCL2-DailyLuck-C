namespace RpCalculator.Core;

/// <summary>
/// 稳定字符串哈希（今日人品算法专用）。
///
/// 公式（严格按需求实现）：
///   初始 hash = 5381
///   对字符串的每个 UTF-16 字符 c：
///       hash = (hash << 5) ^ hash ^ c
///   处理完所有字符后，最终 hash 与常量 0xA98F501BC684032F 进行异或。
///
/// C# 使用 <see cref="long"/> 承载 64 位位模式；它与 Python 中 stable_hash 返回的
/// 64 位无符号整数具有完全相同的二进制位。需要按 Python 的无符号数值参与浮点运算时，
/// 请使用 <c>unchecked((ulong)hash)</c> 转换。
///
/// 所有移位/异或/加法都在 unchecked 上下文中执行，避免 long 溢出抛异常。
/// </summary>
public static class StableHash
{
    /// <summary>最终异或常量（与 Python 的 0xA98F501BC684032F 位模式一致）。</summary>
    public const long XorConstant = unchecked((long)0xA98F501BC684032FUL);

    /// <summary>
    /// 计算完整稳定哈希：先跑完字符，再异或常量。
    /// </summary>
    public static long ComputeHash(ReadOnlySpan<char> text)
    {
        long hash = ContinueHash(5381, text);
        return unchecked(hash ^ XorConstant);
    }

    /// <summary>
    /// 从一个已有状态继续哈希。注意：这里不执行最终异或，返回的是中间状态。
    /// 这样可以让“识别码 + 年份 + 日序”复用前导哈希状态，避免重复计算。
    /// </summary>
    public static long ContinueHash(long hash, ReadOnlySpan<char> text)
    {
        foreach (char c in text)
        {
            // 等价于 hash * 33 ^ c（由于 (h<<5) ^ h = h*33 在无符号位模式下），
            // 这里保持需求原样写法。
            hash = unchecked((hash << 5) ^ hash ^ c);
        }

        return hash;
    }
}
