using System.Globalization;

namespace RpCalculator.Core;

/// <summary>
/// 原始算法逐日验算器。
///
/// 设计目的：用于独立验证主扫描器 <see cref="RpScanner"/> 的结果是否正确。
/// 因此这里 **故意不** 使用主扫描器的任何性能优化：
///   - 不复用任何哈希状态（每次对每一天都从 5381 开始重新计算整个种子的哈希）；
///   - 不按年份缓存 dayOfYear / day 字符串；
///   - 不跳过任何一天；
///   - 字符串拼接顺序与项目根目录 Test.py 完全一致。
///
/// 实现严格等价于 Test.py：
/// <code>
/// HASH_XOR = 0xA98F501BC684032F
///
/// def stable_hash(s: str) -> int:
///     h = 5381
///     for c in s:
///         h = ((h &lt;&lt; 5) ^ h ^ ord(c)) &amp; ((1 &lt;&lt; 64) - 1)
///     return h ^ HASH_XOR
///
/// def score_for_date(d: date, identifier: str) -> int:
///     first_seed = f"asdfgbn{d.timetuple().tm_yday}12#3$45{d.year}IUY"
///     second_seed = f"QWERTY{identifier}0*8&amp;6{d.day}kjhg"
///     first_hash = stable_hash(first_seed) / 3
///     second_hash = stable_hash(second_seed) / 3
///     raw = abs((first_hash + second_hash) / 527) % 1001
///     rounded = round_even(raw)
///     return 100 if rounded &gt;= 970 else round_even(rounded / 969 * 99)
/// </code>
///
/// C# 实现要求：
///   - 64 位哈希以 <see cref="long"/> 承载 Python 无符号整数的位模式；
///   - 所有移位/异或/加法在 <c>unchecked</c> 上下文中执行（不抛 OverflowException）；
///   - 参与浮点运算前，把 long 按 <c>unchecked((ulong)hash)</c> 转为 Python 的无符号数值；
///   - 100 分判断使用 BankersRound（<c>Math.Round(value, MidpointRounding.ToEven)</c>）。
/// </summary>
public static class RawVerifier
{
    /// <summary>等价于 Python 的 <c>XOR_CONST = 0xA98F501BC684032F</c>（位模式）。</summary>
    public const long XorConstant = unchecked((long)0xA98F501BC684032FUL);

    /// <summary>
    /// 验算单个识别码在给定窗口内的“最大间隔”和“100 分日期”列表。
    /// 每次调用都是独立、完整的逐日计算，不依赖任何缓存。
    /// </summary>
    /// <param name="id">识别码（任意字符串，内部会按规则拼接；通常使用 4-4-4-4 格式）。</param>
    /// <param name="startDate">窗口起始日（含）。</param>
    /// <param name="days">窗口天数，必须 ≥ 1。</param>
    public static RawVerificationResult CheckId(string id, DateTime startDate, int days)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "窗口天数必须大于 0。");
        }

        // 与参考实现一致：起始日取日期部分（去除时间分量）。
        var start = startDate.Date;
        var hundredDates = new List<string>();
        var lastIndex = -1;
        var maxGap = 0;

        for (var i = 0; i < days; i++)
        {
            var d = start.AddDays(i);

            // Python：str(d.year)、str(d.timetuple().tm_yday)、str(d.day)
            // 这里的 ToString 使用 InvariantCulture，与参考实现的 str() 行为一致。
            var year = d.Year.ToString(CultureInfo.InvariantCulture);
            var doy = d.DayOfYear.ToString(CultureInfo.InvariantCulture);
            var day = d.Day.ToString(CultureInfo.InvariantCulture);

            // 字符串拼接顺序与 Test.py 完全一致。
            var firstSeed = $"asdfgbn{doy}12#3$45{year}IUY";
            var secondSeed = $"QWERTY{id}0*8&6{day}kjhg";

            var h1 = StableHashOriginal(firstSeed);
            var h2 = StableHashOriginal(secondSeed);

            if (IsHundred(h1, h2))
            {
                if (lastIndex >= 0)
                {
                    var gap = i - lastIndex;
                    if (gap > maxGap)
                    {
                        maxGap = gap;
                    }
                }

                lastIndex = i;
                hundredDates.Add(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }

        return new RawVerificationResult
        {
            Id = id,
            MaxGap = maxGap,
            HundredCount = hundredDates.Count,
            HundredDates = hundredDates
        };
    }

    /// <summary>
    /// 与 Python 参考 1:1 等价的“完整字符串 → 64 位哈希”。
    /// 返回 long 位模式，与 Python 的 64 位无符号整数一致。
    /// </summary>
    public static long StableHashOriginal(string text)
    {
        long hash = 5381;
        foreach (var c in text)
        {
            // 等价于 Python 的 ((h << 5) ^ h ^ ord(c)) & MASK，
            // 但因为 long 是 64 位，C# 移位/异或本身就在 64 位空间回绕，等价于 & ((1<<64)-1)。
            hash = unchecked((hash << 5) ^ hash ^ c);
        }

        // h ^= XOR_CONST（仍是 unchecked），等价于 Python 的 h ^= 0xA98F501BC684032F。
        return unchecked(hash ^ XorConstant);
    }

    /// <summary>
    /// 与 Test.py 的 <c>score_for_date</c> 中 <c>rounded &gt;= 970</c> 等价。
    /// </summary>
    public static bool IsHundred(long h1, long h2)
    {
        ulong u1 = unchecked((ulong)h1);
        ulong u2 = unchecked((ulong)h2);

        double firstHashValue = u1 / 3.0;
        double secondHashValue = u2 / 3.0;
        double raw = Math.Abs((firstHashValue + secondHashValue) / 527.0) % 1001.0;
        int rounded = (int)Math.Round(raw, MidpointRounding.ToEven);

        return rounded >= 970;
    }
}

/// <summary>原始算法验算的完整结果。</summary>
public sealed class RawVerificationResult
{
    public required string Id { get; init; }

    public int MaxGap { get; init; }

    public int HundredCount { get; init; }

    /// <summary>100 分日期列表，格式严格为 <c>yyyy-MM-dd</c>。</summary>
    public IReadOnlyList<string> HundredDates { get; init; } = Array.Empty<string>();
}
