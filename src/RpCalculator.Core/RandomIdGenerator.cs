using System.Collections;
using System.Security.Cryptography;

namespace RpCalculator.Core;

/// <summary>
/// 惰性随机识别码生成器。
///
/// 固定格式（见 <see cref="IdFormat"/>）：
/// - 16 位十六进制字符（大写 0-9 / A-F），按 4-4-4-4 分组、段间用 '-' 连接；
/// - 第一个字符从 "123456789ABCDEF"（排除 '0'）中随机选择，
///   保证识别码不会被另一个应用的前导零规范化逻辑改写为不同值；
/// - 其余 15 个字符从 "0123456789ABCDEF" 中随机选择。
///
/// 使用 Xoshiro256**（快速非加密伪随机）保证 100 亿级别生成性能。
/// 种子仍由加密 RNG 生成。生成器是惰性流：调用方 Take/TakeLong 时才会产生 N 个字符串。
///
/// 性能：每个识别码只需一次 Xoshiro256** 调用即可取得全部 16 个 nibble；
/// 仅当首 nibble 恰为 0 时（概率 1/16）多调用一次，将其强制替换为 1-15 的随机值，
/// 避免整体重试带来的额外开销。
/// </summary>
public sealed class RandomIdGenerator : IEnumerable<string>
{
    private const int GroupLength = 4;
    private const int GroupCount = 4;

    private readonly ulong _seed;

    public RandomIdGenerator()
        : this(CreateSeed())
    {
    }

    /// <summary>以固定种子构造（用于测试复现）。</summary>
    public RandomIdGenerator(ulong seed)
    {
        _seed = seed;
    }

    public IEnumerator<string> GetEnumerator()
    {
        return Generate().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private IEnumerable<string> Generate()
    {
        var rng = new Xoshiro256StarStar(_seed);
        var buffer = new char[GroupCount * GroupLength + (GroupCount - 1)]; // 19

        while (true)
        {
            // 一次调用得到 64 位随机数，正好覆盖 16 个 nibble（16 位十六进制）。
            var v = rng.NextUInt64();

            // 最高 nibble 作为首字符；若为 0 则强制替换为 1-15 中的随机值。
            var first = (int)(v >> 60) & 0xF;
            if (first == 0)
            {
                first = 1 + (int)(rng.NextUInt64() % 15);
            }

            var pos = 0;
            for (var group = 0; group < GroupCount; group++)
            {
                if (group > 0)
                {
                    buffer[pos++] = '-';
                }

                for (var j = 0; j < GroupLength; j++)
                {
                    var shift = 60 - (group * GroupLength + j) * 4;
                    var nibble = (int)(v >> shift) & 0xF;

                    if (group == 0 && j == 0)
                    {
                        nibble = first;
                    }

                    buffer[pos++] = IdFormat.ToHexChar(nibble);
                }
            }

            yield return new string(buffer, 0, pos);
        }
    }

    private static ulong CreateSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt64(bytes);
    }
}

/// <summary>Xoshiro256** 实现（公开以便测试）。</summary>
public sealed class Xoshiro256StarStar
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public Xoshiro256StarStar(ulong seed)
    {
        var sm = new SplitMix64(seed);
        _s0 = sm.Next();
        _s1 = sm.Next();
        _s2 = sm.Next();
        _s3 = sm.Next();
    }

    public ulong NextUInt64()
    {
        var result = RotateLeft(_s1 * 5UL, 7) * 9UL;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }

    private static ulong RotateLeft(ulong value, int offset)
    {
        return (value << offset) | (value >> (64 - offset));
    }

    private sealed class SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed)
        {
            _state = seed;
        }

        public ulong Next()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
