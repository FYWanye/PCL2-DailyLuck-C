namespace RpCalculator.Core;

/// <summary>
/// 单个识别码的“今日人品”扫描器。
///
/// 内存设计：
/// - <see cref="ScanCore"/> 只返回标量（最大间隔、100 分次数），绝不保存每日结果。
/// - <see cref="ScanWithDates"/> 仅在识别码已经成为候选最佳时调用，用于收集最终展示所需日期。
///   它在单次扫描中仍不保存每日人品值，只保存 100 分日期（约 3% 的日期，通常只有几十个）。
/// </summary>
public static class RpScanner
{
    private const long Modulus = 527527L;
    private const long HundredThreshold = 510927L;

    /// <summary>
    /// 等价整数判断：
    /// Q = abs(h1 / 3 + h2 / 3)
    /// 若 Q % 527527 &gt;= 510927，则该日人品值为 100。
    /// </summary>
    public static bool IsHundredPoint(long h1, long h2)
    {
        long q = unchecked((h1 / 3) + (h2 / 3));
        ulong absQ = AbsAsUInt64(q);
        return absQ % Modulus >= HundredThreshold;
    }

    /// <summary>
    /// 只扫描标量结果。这是并行处理的主路径，内存占用 O(1)。
    /// </summary>
    public static RpCoreScanResult ScanCore(string id, DateRangeInfo info)
    {
        // 识别码前缀只算一次：stateId = HashContinue(5381, id)
        long stateId = StableHash.ContinueHash(5381, id.AsSpan());

        var last100Index = -1;
        var maxGap = 0;
        var hundredCount = 0;

        foreach (var yearGroup in info.YearGroups)
        {
            // 年份前缀只算一次。
            long stateYear = StableHash.ContinueHash(stateId, yearGroup.YearString.AsSpan());

            foreach (var entry in yearGroup.Entries)
            {
                // s1 = HashContinue(stateYear, dayOfYear)
                // h1 = s1 ^ XOR_CONST
                long s1 = StableHash.ContinueHash(stateYear, entry.DayOfYearString.AsSpan());
                long h1 = unchecked(s1 ^ StableHash.XorConstant);

                // s2 = HashContinue(s1, day)
                // h2 = s2 ^ XOR_CONST
                long s2 = StableHash.ContinueHash(s1, entry.DayString.AsSpan());
                long h2 = unchecked(s2 ^ StableHash.XorConstant);

                if (!IsHundredPoint(h1, h2))
                {
                    continue;
                }

                hundredCount++;

                if (last100Index >= 0)
                {
                    int gap = entry.DateIndex - last100Index;
                    if (gap > maxGap)
                    {
                        maxGap = gap;
                    }
                }

                last100Index = entry.DateIndex;
            }
        }

        return new RpCoreScanResult(maxGap, hundredCount);
    }

    /// <summary>
    /// “距今最久”模式：从窗口起始日向后扫描，找到第一个 100 分日期立即返回。
    ///
    /// 早停优化：一旦命中第一个 100 分，就不再计算该识别码后续任何日期，
    /// 因此该模式的计算量远小于完整扫描。
    /// </summary>
    /// <param name="visitDay">测试用回调：每扫描一天调用一次（可选）。</param>
    public static RpFirst100ScanResult ScanFirst100(string id, DateRangeInfo info, Action<int>? visitDay = null)
    {
        long stateId = StableHash.ContinueHash(5381, id.AsSpan());

        foreach (var yearGroup in info.YearGroups)
        {
            long stateYear = StableHash.ContinueHash(stateId, yearGroup.YearString.AsSpan());

            foreach (var entry in yearGroup.Entries)
            {
                visitDay?.Invoke(entry.DateIndex);

                long s1 = StableHash.ContinueHash(stateYear, entry.DayOfYearString.AsSpan());
                long h1 = unchecked(s1 ^ StableHash.XorConstant);
                long s2 = StableHash.ContinueHash(s1, entry.DayString.AsSpan());
                long h2 = unchecked(s2 ^ StableHash.XorConstant);

                if (IsHundredPoint(h1, h2))
                {
                    return new RpFirst100ScanResult(true, entry.DateIndex, info.GetDate(entry.DateIndex));
                }
            }
        }

        return new RpFirst100ScanResult(false, -1, default);
    }

    /// <summary>
    /// 完整扫描，收集 100 分日期列表。仅在需要展示时对少数候选调用。
    /// </summary>
    public static RpScanResult ScanWithDates(string id, DateRangeInfo info)
    {
        long stateId = StableHash.ContinueHash(5381, id.AsSpan());

        var hundredDates = new List<DateTime>();
        var last100Index = -1;
        var maxGap = 0;

        foreach (var yearGroup in info.YearGroups)
        {
            long stateYear = StableHash.ContinueHash(stateId, yearGroup.YearString.AsSpan());

            foreach (var entry in yearGroup.Entries)
            {
                long s1 = StableHash.ContinueHash(stateYear, entry.DayOfYearString.AsSpan());
                long h1 = unchecked(s1 ^ StableHash.XorConstant);
                long s2 = StableHash.ContinueHash(s1, entry.DayString.AsSpan());
                long h2 = unchecked(s2 ^ StableHash.XorConstant);

                if (!IsHundredPoint(h1, h2))
                {
                    continue;
                }

                hundredDates.Add(info.GetDate(entry.DateIndex));

                if (last100Index >= 0)
                {
                    int gap = entry.DateIndex - last100Index;
                    if (gap > maxGap)
                    {
                        maxGap = gap;
                    }
                }

                last100Index = entry.DateIndex;
            }
        }

        return new RpScanResult
        {
            Id = id,
            MaxGap = maxGap,
            HundredCount = hundredDates.Count,
            HundredDates = hundredDates
        };
    }

    /// <summary>
    /// 安全的 64 位绝对值，返回 ulong，避免 Math.Abs(long.MinValue) 溢出异常。
    /// 对于 long.MinValue，数学绝对值 2^63 无法放入 long，但可以放入 ulong。
    /// </summary>
    private static ulong AbsAsUInt64(long value)
    {
        if (value >= 0)
        {
            return (ulong)value;
        }

        // -(value + 1) 不会溢出；再 +1 得到 |value|。
        return unchecked((ulong)(-(value + 1)) + 1UL);
    }
}
