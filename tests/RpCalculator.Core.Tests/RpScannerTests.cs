using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

public sealed class RpScannerTests
{
    [Fact]
    public void ScanCore_MatchesScanWithDates()
    {
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 1780);
        string[] ids = ["abc", "test", "123456", "hello", "今日人品"];

        foreach (var id in ids)
        {
            var core = RpScanner.ScanCore(id, info);
            var full = RpScanner.ScanWithDates(id, info);

            Assert.Equal(core.MaxGap, full.MaxGap);
            Assert.Equal(core.HundredCount, full.HundredCount);
            Assert.Equal(full.HundredCount, full.HundredDates.Count);
        }
    }

    [Fact]
    public void ScanWithDates_ComputesAdjacentGapCorrectly()
    {
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 1780);
        var full = RpScanner.ScanWithDates("abc", info);

        if (full.HundredDates.Count < 2)
        {
            Assert.Equal(0, full.MaxGap);
            return;
        }

        var expectedMaxGap = 0;
        for (var i = 1; i < full.HundredDates.Count; i++)
        {
            var gap = (full.HundredDates[i] - full.HundredDates[i - 1]).Days;
            Assert.True(gap > 0, "100 分日期应按时间升序排列。");
            expectedMaxGap = Math.Max(expectedMaxGap, gap);
        }

        Assert.Equal(expectedMaxGap, full.MaxGap);
    }

    [Fact]
    public void DateRangeInfo_GroupsAndIndexesAreCorrect()
    {
        var start = new DateTime(2024, 12, 30);
        var info = new DateRangeInfo(start, 5);

        Assert.Equal(5, info.Days);
        Assert.Equal(2, info.YearGroups.Count);
        Assert.Equal(2024, info.YearGroups[0].Year);
        Assert.Equal(2025, info.YearGroups[1].Year);
        Assert.Equal(2, info.YearGroups[0].Entries.Count);
        Assert.Equal(3, info.YearGroups[1].Entries.Count);
        Assert.Equal(start.AddDays(4), info.GetDate(4));
    }

    [Fact]
    public void ScanFirst100_MatchesFirstDateOfFullScan()
    {
        // 距今最久模式的“第一个 100 分日期”必须与全量扫描的首个 100 分日期一致。
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 1780);
        string[] ids = ["abc", "test", "123456", "hello", "今日人品"];

        foreach (var id in ids)
        {
            var first = RpScanner.ScanFirst100(id, info);
            var full = RpScanner.ScanWithDates(id, info);

            if (!first.Found)
            {
                Assert.Empty(full.HundredDates);
                continue;
            }

            Assert.NotEmpty(full.HundredDates);
            Assert.Equal(full.HundredDates[0], first.Date);

            // DateIndex 应等于窗口起始日到该日期的天数。
            Assert.Equal((first.Date - info.StartDate).Days, first.DateIndex);
        }
    }

    [Fact]
    public void ScanFirst100_ReturnsNotFound_WhenNoHundredInWindow()
    {
        // “test”在 2024-01-01 不是 100 分（见 HundredPointTests 已知向量）。
        // 窗口仅 1 天时，它没有任何 100 分日期，应返回无效结果。
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 1);
        var result = RpScanner.ScanFirst100("test", info);

        Assert.False(result.Found);
        Assert.Equal(-1, result.DateIndex);
    }

    [Fact]
    public void ScanFirst100_EarlyStops_VisitsOnlyUpToFirstHundred()
    {
        // 早停验证核心：visitDay 回调收集每次扫描的日期索引。
        // 若命中，访问过的日期必须恰好是 0..DateIndex（绝不扫描命中之后的日期）；
        // 若未命中，才访问全部窗口。
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 1780);
        var visited = new List<int>();

        var result = RpScanner.ScanFirst100("abc", info, visited.Add);

        if (result.Found)
        {
            Assert.Equal(result.DateIndex + 1, visited.Count);
            Assert.Equal(
                Enumerable.Range(0, result.DateIndex + 1).ToList(),
                visited.ToList());
            Assert.Equal(visited.Max(), result.DateIndex);
        }
        else
        {
            Assert.Equal(1780, visited.Count);
        }
    }

    [Fact]
    public void ScanFirst100_FirstDayHit_VisitsExactlyOneDay()
    {
        // “abc”在 2024-01-01 是 100 分（HundredPointTests 已知向量），
        // 窗口仅 1 天时第 0 天即命中，访问次数必须为 1 —— 最极端情况的早停。
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 1);
        var visited = new List<int>();

        var result = RpScanner.ScanFirst100("abc", info, visited.Add);

        Assert.True(result.Found);
        Assert.Equal(0, result.DateIndex);
        Assert.Equal(new DateTime(2024, 1, 1), result.Date);
        Assert.Equal(new List<int> { 0 }, visited.ToList());
    }

    [Fact]
    public void ScanFirst100_EarlyStop_WorksAcrossYearBoundary()
    {
        // 跨年窗口：2024-12-30 起 5 天（2024 年 2 天 + 2025 年 3 天）。
        // 早停与 DateIndex 在按年分组的日期结构上仍须正确。
        var start = new DateTime(2024, 12, 30);
        var info = new DateRangeInfo(start, 5);
        var visited = new List<int>();

        var result = RpScanner.ScanFirst100("abc", info, visited.Add);

        if (result.Found)
        {
            Assert.Equal(result.DateIndex + 1, visited.Count);
            Assert.Equal(
                Enumerable.Range(0, result.DateIndex + 1).ToList(),
                visited.ToList());
            Assert.Equal(start.AddDays(result.DateIndex), result.Date);
        }
        else
        {
            Assert.Equal(5, visited.Count);
        }
    }
}
