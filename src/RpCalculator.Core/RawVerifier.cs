using System.Globalization;

namespace RpCalculator.Core;

/// <summary>
/// 原始算法逐日验算器。
///
/// 设计目的：用于独立验证主扫描器 <see cref="RpScanner"/> 的结果是否正确。
/// 因此这里 **故意不** 使用主扫描器的任何性能优化：
///   - 不复用识别码前缀的哈希状态（每次对每一天都从 5381 开始重新计算整个字符串的哈希）；
///   - 不按年份缓存 dayOfYear / day 字符串；
///   - 不跳过任何一天；
///   - 字符串拼接 <c>rid + year + doy</c> 与 <c>rid + year + doy + day</c> 与参考 Python 实现逐字符等价。
///
/// 实现严格等价于下面的 Python 参考实现：
/// <code>
/// XOR_CONST = 0xA98F501BC684032F
/// MOD = 527527
/// THRESHOLD = 510927
/// MASK = (1 &lt;&lt; 64) - 1
///
/// def stable_hash(s: str) -&gt; int:
///     h = 5381
///     for c in s:
///         h = ((h &lt;&lt; 5) ^ h ^ ord(c)) &amp; MASK
///     h ^= XOR_CONST
///     if h &gt;= (1 &lt;&lt; 63):
///         h -= (1 &lt;&lt; 64)
///     return h
///
/// def is_hundred(h1: int, h2: int) -&gt; bool:
///     q = h1 // 3 + h2 // 3
///     if q &lt; 0: q = -q
///     return q % MOD &gt;= THRESHOLD
///
/// def check_id(rid, start, days):
///     start_date = datetime.strptime(start, "%Y-%m-%d")
///     hundred_dates, last_idx, max_gap = [], None, 0
///     for i in range(days):
///         d = start_date + timedelta(days=i)
///         year, doy, day = str(d.year), str(d.timetuple().tm_yday), str(d.day)
///         h1 = stable_hash(rid + year + doy)
///         h2 = stable_hash(rid + year + doy + day)
///         if is_hundred(h1, h2):
///             if last_idx is not None:
///                 gap = i - last_idx
///                 if gap &gt; max_gap: max_gap = gap
///             last_idx = i
///             hundred_dates.append(d.strftime("%Y-%m-%d"))
///     return rid, max_gap, hundred_dates
/// </code>
///
/// C# 实现要求：
///   - 64 位有符号 long 存储哈希结果；
///   - 所有移位/异或/加法在 <c>unchecked</c> 上下文中执行（不抛 OverflowException）；
///   - <c>long.MinValue</c> 绝对值用 ulong 承载以避免 <c>Math.Abs</c> 抛异常；
///   - 100 分判断使用整数等价式 <c>abs(h1/3 + h2/3) % 527527 &gt;= 510927</c>。
/// </summary>
public static class RawVerifier
{
    /// <summary>等价于 Python 的 <c>XOR_CONST = 0xA98F501BC684032F</c>。</summary>
    public const long XorConstant = unchecked((long)0xA98F501BC684032FUL);

    /// <summary>等价于 Python 的 <c>MOD = 527527</c>。</summary>
    public const long Modulus = 527527L;

    /// <summary>等价于 Python 的 <c>THRESHOLD = 510927</c>。</summary>
    public const long HundredThreshold = 510927L;

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

            // 字符串拼接顺序与 Python 参考完全一致：rid + year + doy(+ day)
            var h1 = StableHashOriginal(id + year + doy);
            var h2 = StableHashOriginal(id + year + doy + day);

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
    /// 与 Python 参考 1:1 等价的“完整字符串 → 最终 64 位有符号哈希”。
    /// 整个过程在 <c>unchecked</c> 上下文中执行，溢出不会抛异常。
    /// </summary>
    public static long StableHashOriginal(string text)
    {
        long hash = 5381;
        foreach (var c in text)
        {
            // 等价于 Python 的 ((h << 5) ^ h ^ ord(c)) & MASK，
            // 但因为 long 是 64 位有符号，C# 移位本身就等价于 & ((1<<64)-1)，
            // 所以这里直接 unchecked 即可。
            hash = unchecked((hash << 5) ^ hash ^ c);
        }

        // h ^= XOR_CONST（仍是 unchecked），等价于 Python 的 h ^= 0xA98F501BC684032F。
        return unchecked(hash ^ XorConstant);
    }

    /// <summary>
    /// 与 Python 参考 1:1 等价的“是否为 100 分”判断：
    /// <c>abs(h1/3 + h2/3) % 527527 &gt;= 510927</c>。
    /// C# 中整数除法向零取整，与 Python 整除 <c>//</c> 行为一致。
    /// </summary>
    public static bool IsHundred(long h1, long h2)
    {
        long q = unchecked((h1 / 3) + (h2 / 3));
        ulong absQ = AbsAsUInt64(q);
        return absQ % (ulong)Modulus >= (ulong)HundredThreshold;
    }

    /// <summary>
    /// 安全 64 位绝对值（ulong 承载），避免 <c>Math.Abs(long.MinValue)</c> 抛 OverflowException。
    /// 与 <see cref="RpScanner.AbsAsUInt64"/> 语义一致。
    /// </summary>
    private static ulong AbsAsUInt64(long value)
    {
        if (value >= 0)
        {
            return (ulong)value;
        }

        // -(value + 1) 不溢出；+1 后得到 |value|。
        return unchecked((ulong)(-(value + 1)) + 1UL);
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
