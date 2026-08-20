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

        long h1 = StableHash.ComputeHash(seed1);
        long h2 = StableHash.ComputeHash(seed2);

        Assert.Equal(expected, RpScanner.IsHundredPoint(h1, h2));
    }

    [Fact]
    public void IsHundredPoint_HandlesNegativeHashesWithoutOverflow()
    {
        // long.MinValue 附近的值不应抛异常。
        var result = RpScanner.IsHundredPoint(long.MinValue, long.MinValue);
        Assert.False(result);
    }
}
