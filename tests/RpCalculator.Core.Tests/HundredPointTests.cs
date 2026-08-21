using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

public sealed class HundredPointTests
{
    // 这些用例由项目根目录 Test.py 独立验证：
    //   种子1 = "asdfgbn" + dayOfYear + "12#3$45" + year + "IUY"
    //   种子2 = "QWERTY" + id + "0*8&6" + day + "kjhg"
    [Theory]
    [InlineData("1484-92B8-1D6E-1F89", 2024, 181, 29, true)]
    [InlineData("0267-CDF6-A2D7-78CD", 2024, 113, 22, true)]
    [InlineData("FC54-2ACD-8A65-C857", 2024, 293, 19, true)]
    [InlineData("158F-D084-FCF0-313B", 2027, 151, 31, true)]
    [InlineData("9301-6878-7A40-C2F3", 2026, 260, 17, true)]
    [InlineData("abc", 2024, 1, 1, false)]
    [InlineData("test", 2024, 1, 1, false)]
    [InlineData("ABCD-EF12-3456-7890", 2026, 232, 20, false)]
    public void IsHundredPoint_MatchesKnownCases(
        string id,
        int year,
        int dayOfYear,
        int day,
        bool expected)
    {
        string seed1 = $"asdfgbn{dayOfYear}12#3$45{year}IUY";
        string seed2 = $"QWERTY{id}0*8&6{day}kjhg";

        ulong h1 = StableHash.ComputeHash(seed1);
        ulong h2 = StableHash.ComputeHash(seed2);

        Assert.Equal(expected, RpScanner.IsHundredPoint(h1, h2));
    }

    [Fact]
    public void IsHundredPoint_HandlesUInt64MaxWithoutOverflow()
    {
        // ulong.MaxValue 不应抛异常。
        var ex = Record.Exception(() => RpScanner.IsHundredPoint(ulong.MaxValue, ulong.MaxValue));
        Assert.Null(ex);
    }

    [Fact]
    public void SpecialCase_158F_D084_FCF0_313B_2027_05_31_IsHundred()
    {
        // 用户指定的回归用例：修复前会因符号/精度问题漏掉该 100 分日期。
        var info = new DateRangeInfo(new DateTime(2027, 5, 31), 1);
        var result = RpScanner.ScanWithDates("158F-D084-FCF0-313B", info);

        Assert.Contains(new DateTime(2027, 5, 31), result.HundredDates);
    }

    [Fact]
    public void RequiredCase_9301_6878_7A40_C2F3_2026_09_17_IsHundred()
    {
        // 追加需求指定的必须验证案例。
        var info = new DateRangeInfo(new DateTime(2026, 9, 17), 1);
        var result = RpScanner.ScanWithDates("9301-6878-7A40-C2F3", info);

        Assert.Contains(new DateTime(2026, 9, 17), result.HundredDates);
    }
}
