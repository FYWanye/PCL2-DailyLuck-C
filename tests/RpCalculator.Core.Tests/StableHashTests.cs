using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

public sealed class StableHashTests
{
    // 这些期望值由独立 Python 实现按需求公式计算得到，
    // 用于锁定 C# 实现的 64 位有符号 long 行为。
    [Theory]
    [InlineData("", -6228671679405222358L)]
    [InlineData("a", -6228671679405050133L)]
    [InlineData("test", -6228671684498533668L)]
    [InlineData("abc", -6228671679307863382L)]
    [InlineData("hello", -6228671589951204408L)]
    [InlineData("1234567890", -177760784874330869L)]
    public void ComputeHash_MatchesKnownVectors(string input, long expected)
    {
        Assert.Equal(expected, StableHash.ComputeHash(input));
    }

    [Fact]
    public void ContinueHash_ThenXor_EqualsComputeHash()
    {
        string[] samples = ["", "a", "abc", "test", "hello", "今日人品", "abc202411"];

        foreach (var sample in samples)
        {
            long state = StableHash.ContinueHash(5381, sample.AsSpan());
            Assert.Equal(StableHash.ComputeHash(sample), state ^ StableHash.XorConstant);
        }
    }
}
