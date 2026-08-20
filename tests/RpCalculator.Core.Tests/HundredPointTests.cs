using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

public sealed class HundredPointTests
{
    // 这些用例由独立 Python 实现验证：种子1 = id + year + dayOfYear，
    // 种子2 = id + year + dayOfYear + day。
    [Theory]
    [InlineData("test", 2024, 1, 1, false)]
    [InlineData("test", 2024, 1, 2, false)]
    [InlineData("abc", 2024, 1, 1, true)]
    [InlineData("abc", 2024, 2, 2, true)]
    [InlineData("123456", 2024, 100, 10, false)]
    public void IsHundredPoint_MatchesKnownCases(
        string id,
        int year,
        int dayOfYear,
        int day,
        bool expected)
    {
        string seed1 = id + year + dayOfYear;
        string seed2 = id + year + dayOfYear + day;

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
