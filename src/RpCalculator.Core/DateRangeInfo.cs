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

    public DateRangeInfo(DateTime startDate, int days)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "窗口天数必须大于 0。");
        }

        StartDate = startDate.Date;
        Days = days;

        var groups = new List<YearGroup>();
        var currentEntries = new List<DayEntry>();
        var currentYear = int.MinValue;

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
                currentEntries = new List<DayEntry>(days / 365 + 2);
            }

            currentEntries.Add(new DayEntry(
                index,
                date.DayOfYear.ToString(CultureInfo.InvariantCulture),
                date.Day.ToString(CultureInfo.InvariantCulture)));
        }

        if (currentEntries.Count > 0)
        {
            groups.Add(new YearGroup(currentYear, currentEntries.ToArray()));
        }

        _yearGroups = groups.ToArray();
        YearGroups = _yearGroups;
    }

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
        Entries = entries;
    }

    public int Year { get; }

    /// <summary>年份的不可变字符串，扫描时复用。</summary>
    public string YearString { get; }

    public IReadOnlyList<DayEntry> Entries { get; }
}

/// <summary>
/// 一天的预计算条目。
/// DateIndex 是从窗口起始日算起的第几天（0 基），相邻 100 分日期的间隔 = DateIndex 之差。
/// </summary>
public readonly struct DayEntry
{
    public DayEntry(int dateIndex, string dayOfYearString, string dayString)
    {
        DateIndex = dateIndex;
        DayOfYearString = dayOfYearString;
        DayString = dayString;
    }

    public int DateIndex { get; }

    /// <summary>date.DayOfYear 的不可变字符串。</summary>
    public string DayOfYearString { get; }

    /// <summary>date.Day 的不可变字符串。</summary>
    public string DayString { get; }
}
