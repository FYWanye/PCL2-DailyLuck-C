namespace RpCalculator.Core;

/// <summary>
/// 单个识别码的“今日人品”扫描器。
///
/// 算法与项目根目录 Test.py 完全一致：
///   种子1 = "asdfgbn" + dayOfYear + "12#3$45" + year + "IUY"
///   种子2 = "QWERTY" + id + "0*8&6" + day + "kjhg"
///   h1 = stable_hash(种子1)
///   h2 = stable_hash(种子2)
///   raw = abs((h1/3.0 + h2/3.0) / 527.0) % 1001.0
///   rounded = BankersRound(raw)
///   score = rounded >= 970 ? 100 : BankersRound(rounded / 969.0 * 99.0)
///
/// 本类只判断 score == 100（即 rounded >= 970）。
///
/// 内存设计：
/// - <see cref="ScanCore"/> 只返回标量（最大间隔、100 分次数），绝不保存每日结果。
/// - <see cref="ScanWithDates"/> 仅在识别码已经成为候选最佳时调用，用于收集最终展示所需日期。
///   它在单次扫描中仍不保存每日人品值，只保存 100 分日期（约 3% 的日期，通常只有几十个）。
/// </summary>
public static class RpScanner
{
    private const string FirstSeedPrefix = "asdfgbn";
    private const string SecondSeedPrefix = "QWERTY";
    private const string SecondSeedMiddle = "0*8&6";
    private const string SecondSeedSuffix = "kjhg";

    /// <summary>第一个种子的固定前缀哈希状态，所有识别码/日期复用。</summary>
    internal static readonly ulong FirstSeedPrefixState =
        StableHash.ContinueHash(5381UL, FirstSeedPrefix.AsSpan());

    /// <summary>
    /// 判断两个种子哈希对应的当日人品是否为 100 分。
    /// 与 Test.py 的 <c>score_for_date</c> 中 <c>rounded &gt;= 970</c> 等价。
    /// </summary>
    public static bool IsHundredPoint(ulong h1, ulong h2)
    {
        double firstHashValue = (double)h1 / 3.0;
        double secondHashValue = (double)h2 / 3.0;
        double raw = Math.Abs((firstHashValue + secondHashValue) / 527.0) % 1001.0;

        // Python 的 h1 / 3 是对 64 位整数做“真除法”，返回正确舍入的 double；
        // C# 直接 (double)h1 / 3.0 属于双重舍入，在 970 阈值或 0/1001 回绕边界
        // 可能差 1 个 ulp，导致 100 分误判。
        // 这里先用快速 double 路径；进入边界危险区后用“整数商+余数”近似复核，
        // 只有近似结果仍然贴近 970 / 0 / 1000 时才走精确 UInt128，避免拖慢距今最久模式。
        if (raw < 10.0 || raw > 990.0 || (raw >= 960.0 && raw <= 980.0))
        {
            firstHashValue = StableHash.DivideBy3Approx(h1);
            secondHashValue = StableHash.DivideBy3Approx(h2);
            raw = Math.Abs((firstHashValue + secondHashValue) / 527.0) % 1001.0;

            if (raw < 2.0 || raw > 998.0 || (raw >= 968.0 && raw <= 972.0))
            {
                firstHashValue = StableHash.DivideBy3ToDouble(h1);
                secondHashValue = StableHash.DivideBy3ToDouble(h2);
                raw = Math.Abs((firstHashValue + secondHashValue) / 527.0) % 1001.0;
            }
        }

        double rounded = Math.Round(raw, MidpointRounding.ToEven);

        return rounded >= 970.0;
    }

    /// <summary>
    /// 只扫描标量结果。这是并行处理的主路径，内存占用 O(1)。
    /// </summary>
    public static RpCoreScanResult ScanCore(string id, DateRangeInfo info)
    {
        // 第二个种子前缀只算一次：QWERTY + id + 0*8&6。
        ulong stateSecondId = GetSecondSeedIdState(id);

        var last100Index = -1;
        var maxGap = 0;
        var hundredCount = 0;

        foreach (var yearGroup in info.YearGroups)
        {
            foreach (var entry in yearGroup.Entries)
            {
                // 种子1已由 DateRangeInfo 按日期预计算，这里直接读取。
                ulong h1 = entry.H1;

                // 种子2：QWERTY + id + 0*8&6 + day + kjhg
                ulong state2 = StableHash.ContinueHash(stateSecondId, entry.DayString.AsSpan());
                state2 = StableHash.ContinueHash(state2, SecondSeedSuffix.AsSpan());
                ulong h2 = state2 ^ StableHash.XorConstant;

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
        ulong stateSecondId = GetSecondSeedIdState(id);

        foreach (var yearGroup in info.YearGroups)
        {
            foreach (var entry in yearGroup.Entries)
            {
                visitDay?.Invoke(entry.DateIndex);

                ulong h1 = entry.H1;

                ulong state2 = StableHash.ContinueHash(stateSecondId, entry.DayString.AsSpan());
                state2 = StableHash.ContinueHash(state2, SecondSeedSuffix.AsSpan());
                ulong h2 = state2 ^ StableHash.XorConstant;

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
        ulong stateSecondId = GetSecondSeedIdState(id);

        var hundredDates = new List<DateTime>();
        var last100Index = -1;
        var maxGap = 0;

        foreach (var yearGroup in info.YearGroups)
        {
            foreach (var entry in yearGroup.Entries)
            {
                ulong h1 = entry.H1;

                ulong state2 = StableHash.ContinueHash(stateSecondId, entry.DayString.AsSpan());
                state2 = StableHash.ContinueHash(state2, SecondSeedSuffix.AsSpan());
                ulong h2 = state2 ^ StableHash.XorConstant;

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
    /// 预计算第二个种子的固定前缀哈希状态：
    /// <c>QWERTY + id + 0*8&6</c>。
    /// </summary>
    private static ulong GetSecondSeedIdState(string id)
    {
        ulong state = StableHash.ContinueHash(5381UL, SecondSeedPrefix.AsSpan());
        state = StableHash.ContinueHash(state, id.AsSpan());
        state = StableHash.ContinueHash(state, SecondSeedMiddle.AsSpan());
        return state;
    }
}
