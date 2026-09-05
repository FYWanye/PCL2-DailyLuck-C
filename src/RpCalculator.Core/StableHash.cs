using System.Numerics;

namespace RpCalculator.Core;

/// <summary>
/// 稳定字符串哈希（今日人品算法专用）。
///
/// 公式（严格按需求实现）：
///   初始 hash = 5381UL
///   对字符串的每个 UTF-16 字符 c：
///       hash = (hash << 5) ^ hash ^ c
///   处理完所有字符后，最终 hash 与常量 0xA98F501BC684032F 进行异或。
///
/// 哈希全程使用 <see cref="ulong"/>，与 Python 中 stable_hash 返回的
/// 64 位无符号整数在数值上完全一致，避免有符号解释导致分数计算偏差。
/// </summary>
public static class StableHash
{
    /// <summary>最终异或常量，与 Python 的 0xA98F501BC684032F 完全一致。</summary>
    public const ulong XorConstant = 0xA98F501BC684032FUL;

    /// <summary>
    /// 计算完整稳定哈希：先跑完字符，再异或常量。
    /// </summary>
    public static ulong ComputeHash(ReadOnlySpan<char> text)
    {
        ulong hash = ContinueHash(5381UL, text);
        return hash ^ XorConstant;
    }

    /// <summary>
    /// 从一个已有状态继续哈希。注意：这里不执行最终异或，返回的是中间状态。
    /// 这样可以让“识别码 + 年份 + 日序”复用前导哈希状态，避免重复计算。
    /// </summary>
    public static ulong ContinueHash(ulong hash, ReadOnlySpan<char> text)
    {
        foreach (char c in text)
        {
            // 等价于 hash * 33 ^ c（由于 (h<<5) ^ h = h*33 在无符号位模式下），
            // 这里保持需求原样写法。
            hash = unchecked((hash << 5) ^ hash ^ c);
        }

        return hash;
    }

    /// <summary>
    /// 精确模拟 Python 的 <c>h / 3</c>（整数真除法 -> 最近 double）。
    /// C# 的 <c>(double)h / 3.0</c> 会先把 h 舍入成 double 再做除法，属于双重舍入，
    /// 在 970/1000/0 边界附近可能与 Python 差 1 个 ulp，导致 100 分误判。
    /// 这里用 53 位定点数做一次正确舍入；只应在边界危险区调用。
    /// </summary>
    internal static double DivideBy3ToDouble(ulong value)
    {
        const double scale = 9007199254740992.0; // 2^53
        var numerator = new BigInteger(value) << 53;
        var quotient = BigInteger.DivRem(numerator, 3, out var remainder);
        if (remainder * 2 >= 3)
        {
            quotient += 1;
        }

        // 注意：不能直接 (double)quotient——.NET 的 BigInteger→double 舍入与 Python 的
        // int→float 不完全一致，在边界会差 1 ulp。这里按 IEEE 位级构造 double，
        // 得到与 Python 一致的“正确舍入到最近 double”。此方法只会在边界危险区被调用。
        return BigIntegerToDouble(quotient) / scale;
    }

    /// <summary>把正 BigInteger 按“最近偶数”正确舍入成 double（等价于 Python int→float）。</summary>
    private static double BigIntegerToDouble(BigInteger value)
    {
        if (value.IsZero)
        {
            return 0.0;
        }

        var bits = (int)value.GetBitLength();
        if (bits <= 53)
        {
            return (double)value;
        }

        var shift = bits - 53;
        var significand = value >> shift;
        var remainder = value & ((BigInteger.One << shift) - 1);
        var half = BigInteger.One << (shift - 1);

        if (remainder > half || (remainder == half && !significand.IsEven))
        {
            significand += 1;
        }

        if (significand.GetBitLength() > 53)
        {
            significand >>= 1;
            bits++;
        }

        var exponent = bits - 1;
        var biasedExponent = exponent + 1023;
        var significandPart = (long)(significand - (BigInteger.One << 52));
        var rawBits = (long)biasedExponent << 52 | significandPart;
        return BitConverter.Int64BitsToDouble(rawBits);
    }
}
