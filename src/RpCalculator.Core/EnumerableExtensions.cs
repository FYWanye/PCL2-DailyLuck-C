namespace RpCalculator.Core;

public static class EnumerableExtensions
{
    /// <summary>
    /// 支持 long 数量的惰性 Take。
    /// 内置 Enumerable.Take 只接受 int，无法覆盖最多 100 亿的需求。
    /// </summary>
    public static IEnumerable<T> TakeLong<T>(this IEnumerable<T> source, long count)
    {
        if (count <= 0)
        {
            yield break;
        }

        long remaining = count;
        foreach (var item in source)
        {
            if (remaining <= 0)
            {
                yield break;
            }

            remaining--;
            yield return item;
        }
    }
}
