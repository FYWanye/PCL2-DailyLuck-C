using System.Globalization;

namespace RpCalculator.Core;

/// <summary>
/// 窗口内所有日期的预计算信息。
///
/// 性能原因：每天只需要 year / dayOfYear / day 三个字符串。
/// 预先按年份分组并缓存字符串，避免扫描每个识别码时反复调用
/// DateTime.Year、DayOfYear、Day 以及 ToString()。
/// </summary>
public sealed class DateRangeInfo
{
    private readonly YearGroup[] _yearGroups;
    private readonly DayEntry[] _allEntries;

    public DateRangeInfo(DateTime startDate, int days)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "窗口天数必须大于 0。");
        }

        StartDate = startDate.Date;
        Days = days;

        var groups = new List<YearGroup>();
        var currentEntries = new List<DayEntry>(days / 365 + 2);
        var allEntries = new List<DayEntry>(days);
        var currentYear = int.MinValue;
        var currentYearSuffix = string.Empty;

        for (var index = 0; index < days; index++)
        {
            var date = StartDate.AddDays(index);

            if (date.Year != currentYear)
            {
                if (currentEntries.Count > 0)
                {
                    groups.Add(new YearGroup(currentYear, currentEntries.ToArray()));
                }

                currentYear = date.Year;
                currentYearSuffix = "12#3$45" + currentYear.ToString(CultureInfo.InvariantCulture) + "IUY";
                currentEntries = new List<DayEntry>(days / 365 + 2);
            }

            var dayOfYearString = date.DayOfYear.ToString(CultureInfo.InvariantCulture);
            var dayString = date.Day.ToString(CultureInfo.InvariantCulture);

            // 种子1只依赖日期，不依赖识别码，因此在这里预计算 h1，
            // 扫描每个识别码时不再重复哈希 dayOfYear + year 后缀。
            var h1State = StableHash.ContinueHash(RpScanner.FirstSeedPrefixState, dayOfYearString.AsSpan());
            h1State = StableHash.ContinueHash(h1State, currentYearSuffix.AsSpan());
            var h1 = h1State ^ StableHash.XorConstant;

            var entry = new DayEntry(index, date.Day, dayOfYearString, dayString, h1);
            currentEntries.Add(entry);
            allEntries.Add(entry);
        }

        if (currentEntries.Count > 0)
        {
            groups.Add(new YearGroup(currentYear, currentEntries.ToArray()));
        }

        _yearGroups = groups.ToArray();
        _allEntries = allEntries.ToArray();
        YearGroups = _yearGroups;
    }

    /// <summary>供核心扫描器使用的一维日期数组，避免嵌套接口枚举。</summary>
    internal DayEntry[] AllEntries => _allEntries;

    public DateTime StartDate { get; }

    public int Days { get; }

    public IReadOnlyList<YearGroup> YearGroups { get; }

    public DateTime GetDate(int dateIndex)
    {
        if ((uint)dateIndex >= (uint)Days)
        {
            throw new ArgumentOutOfRangeException(nameof(dateIndex));
        }

        return StartDate.AddDays(dateIndex);
    }
}

/// <summary>某一年份的日期条目。</summary>
public sealed class YearGroup
{
    public YearGroup(int year, DayEntry[] entries)
    {
        Year = year;
        YearString = year.ToString(CultureInfo.InvariantCulture);
        // 第一个种子中 dayOfYear 之后的部分：12#3$45 + year + IUY。
        // 按年份缓存，避免每个识别码、每天重复拼接。
        FirstSeedSuffix = "12#3$45" + YearString + "IUY";
        Entries = entries;
    }

    public int Year { get; }

    /// <summary>年份的不可变字符串，扫描时复用。</summary>
    public string YearString { get; }

    /// <summary>第一个种子中 dayOfYear 之后的不可变后缀，扫描时复用。</summary>
    public string FirstSeedSuffix { get; }

    public IReadOnlyList<DayEntry> Entries { get; }
}

/// <summary>
/// 一天的预计算条目。
/// DateIndex 是从窗口起始日算起的第几天（0 基），相邻 100 分日期的间隔 = DateIndex 之差。
/// </summary>
public readonly struct DayEntry
{
    public DayEntry(int dateIndex, string dayOfYearString, string dayString, ulong h1)
        : this(dateIndex, ExtractDay(dayString), dayOfYearString, dayString, h1)
    {
    }

    public DayEntry(int dateIndex, int day, string dayOfYearString, string dayString, ulong h1)
    {
        DateIndex = dateIndex;
        Day = day;
        DayOfYearString = dayOfYearString;
        DayString = dayString;
        H1 = h1;
    }

    public int DateIndex { get; }

    /// <summary>date.Day 的数值（1-31），扫描时用于避免重复字符串访问。</summary>
    public int Day { get; }

    /// <summary>date.DayOfYear 的不可变字符串。</summary>
    public string DayOfYearString { get; }

    /// <summary>date.Day 的不可变字符串。</summary>
    public string DayString { get; }

    /// <summary>种子1的最终 h1（已异或 XorConstant），只依赖日期，不依赖识别码。</summary>
    public ulong H1 { get; }

    private static int ExtractDay(string dayString)
    {
        return int.Parse(dayString, System.Globalization.CultureInfo.InvariantCulture);
    }
}
