using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

/// <summary>
/// 随机识别码生成器测试：固定 16 位十六进制 4-4-4-4 格式，
/// 且第一个字符必须来自 "123456789ABCDEF"（排除 '0'）。
/// </summary>
public sealed class RandomIdGeneratorTests
{
    [Fact]
    public void Generate_FirstCharNeverZero()
    {
        var ids = new RandomIdGenerator(42).Take(20_000).ToList();

        Assert.Equal(20_000, ids.Count);
        foreach (var id in ids)
        {
            // 首字符属于 1-9 / A-F，绝不可能是 '0'。
            var first = id[0];
            Assert.True(
                (first >= '1' && first <= '9') || (first >= 'A' && first <= 'F'),
                $"首字符 '{first}' 不在 1-9 / A-F 范围内：{id}");
        }
    }

    [Fact]
    public void Generate_AllIdsAreValidStandardFormat()
    {
        var ids = new RandomIdGenerator(12345).Take(5_000).ToList();

        foreach (var id in ids)
        {
            Assert.True(IdFormat.IsValidId(id), $"非法识别码：{id}");
            // 格式必须严格为 XXXX-XXXX-XXXX-XXXX。
            Assert.Equal(19, id.Length);
            Assert.Equal('-', id[4]);
            Assert.Equal('-', id[9]);
            Assert.Equal('-', id[14]);
        }
    }

    [Fact]
    public void Generate_ProducesAllHexChars()
    {
        // 覆盖度检查：排除首字符后，其余字符应覆盖完整十六进制字母表。
        var ids = new RandomIdGenerator(7).Take(10_000).ToList();
        var seen = new HashSet<char>();

        foreach (var id in ids)
        {
            foreach (var c in id)
            {
                if (c != '-')
                {
                    seen.Add(c);
                }
            }
        }

        foreach (var c in IdFormat.HexChars)
        {
            Assert.Contains(c, seen);
        }
    }

    [Fact]
    public void Generate_IsLazyAndDeterministicWithSeed()
    {
        var a = new RandomIdGenerator(999).Take(10).ToArray();
        var b = new RandomIdGenerator(999).Take(10).ToArray();

        // 相同种子 → 相同序列（便于测试复现）。
        Assert.Equal(a, b);
    }

    [Fact]
    public void Generate_TakeLong_SupportsLargeCounts()
    {
        var generator = new RandomIdGenerator(5);
        var count = generator.TakeLong(100).Count();
        Assert.Equal(100, count);
    }
}
