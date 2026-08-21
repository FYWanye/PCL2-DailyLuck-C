using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

public sealed class StableHashTests
{
    // 这些期望值由项目根目录 Test.py 独立验证，
    // 用于锁定 C# 实现的 64 位无符号 ulong 行为。
    [Theory]
    [InlineData("", 12218072394304329258UL)]
    [InlineData("a", 12218072394304501483UL)]
    [InlineData("test", 12218072389211017948UL)]
    [InlineData("abc", 12218072394401688234UL)]
    [InlineData("hello", 12218072483758347208UL)]
    [InlineData("1234567890", 18268983288835220747UL)]
    public void ComputeHash_MatchesKnownVectors(string input, ulong expected)
    {
        Assert.Equal(expected, StableHash.ComputeHash(input));
    }

    [Fact]
    public void ContinueHash_ThenXor_EqualsComputeHash()
    {
        string[] samples = ["", "a", "abc", "test", "hello", "今日人品", "abc202411"];

        foreach (var sample in samples)
        {
            ulong state = StableHash.ContinueHash(5381UL, sample.AsSpan());
            Assert.Equal(StableHash.ComputeHash(sample), state ^ StableHash.XorConstant);
        }
    }
}
