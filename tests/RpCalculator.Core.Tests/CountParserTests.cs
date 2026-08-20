using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

public sealed class CountParserTests
{
    [Theory]
    [InlineData("10000000000", 10_000_000_000L)]
    [InlineData("1e10", 10_000_000_000L)]
    [InlineData("1E10", 10_000_000_000L)]
    [InlineData("1,000,000", 1_000_000L)]
    [InlineData("500", 500L)]
    public void TryParse_AcceptsLargeCounts(string text, long expected)
    {
        Assert.True(CountParser.TryParse(text, out var count));
        Assert.Equal(expected, count);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData("1e100")]
    public void TryParse_RejectsInvalidValues(string text)
    {
        Assert.False(CountParser.TryParse(text, out _));
    }
}
