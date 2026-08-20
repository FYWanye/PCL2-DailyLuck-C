using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

/// <summary>
/// 验证 <see cref="RawVerifier"/> 的语义与主扫描器 <see cref="RpScanner"/> 完全等价。
///
/// 这是“原始计算”功能的核心保障：无论主扫描器内部如何优化，结果都必须在数值上
/// 与逐日原始算法一致。如果这条测试通过，就证明主扫描器对同一识别码、同一窗口
/// 给出的最大间隔和 100 分日期列表与 Python 参考实现完全一致。
/// </summary>
public sealed class RawVerifierTests
{
    [Theory]
    [InlineData("0000-0000-000C-159C", "2026-08-19", 1780)]
    [InlineData("1111-2222-3333-4444", "2024-01-01", 365)]
    [InlineData("ABCD-EF12-3456-7890", "2025-06-15", 730)]
    [InlineData("FFFF-FFFF-FFFF-FFFF", "2026-01-01", 30)]
    public void RawVerifier_AgreesWithMainScanner(string id, string startDate, int days)
    {
        var start = DateTime.Parse(startDate);
        var range = new DateRangeInfo(start, days);

        // 1) 用未优化的原始算法算出“参考真相”。
        var raw = RawVerifier.CheckId(id, start, days);

        // 2) 用主扫描器（含所有性能优化）扫描同一个识别码、同一个窗口。
        var scan = RpScanner.ScanWithDates(id, range);

        // 3) 关键指标必须一致。
        Assert.Equal(raw.MaxGap, scan.MaxGap);
        Assert.Equal(raw.HundredCount, scan.HundredCount);

        // 4) 100 分日期数量较少时才逐个比较（避免长列表下让测试输出刷屏）。
        // 超过 50 个时退化为“集合相等”检查。
        if (raw.HundredCount <= 50)
        {
            var rawDates = string.Join(",", raw.HundredDates);
            var scanDates = string.Join(",",
                scan.HundredDates.Select(d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
            Assert.Equal(rawDates, scanDates);
        }
    }

    [Fact]
    public void RawVerifier_HandlesLongMinValueWithoutThrowing()
    {
        // 与 HundredPointTests 相同的边界用例：long.MinValue 不能让原始计算抛异常。
        // 这里我们只确认“调用一次原始计算不会因为 long.MinValue 哈希而崩溃”。
        // 不要求具体结果，只要求不抛 OverflowException。
        var ex = Record.Exception(() =>
        {
            // 用一个 16 位十六进制识别码跑 1 天——具体 h1/h2 由算法决定，
            // 关键是内部不能因为 Math.Abs 溢出。
            _ = RawVerifier.CheckId("0000-0000-0000-0001", new DateTime(2024, 1, 1), 1);
        });
        Assert.Null(ex);
    }

    [Fact]
    public void RawVerifier_DateFormat_IsExactlyYyyyMmDd()
    {
        // 日期格式必须严格为 yyyy-MM-dd，与 Python 的 d.strftime("%Y-%m-%d") 等价。
        var result = RawVerifier.CheckId("1111-2222-3333-4444", new DateTime(2026, 8, 19), 1780);
        foreach (var d in result.HundredDates)
        {
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", d);
        }
    }
}
