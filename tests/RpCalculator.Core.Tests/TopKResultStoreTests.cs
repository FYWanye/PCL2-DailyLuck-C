using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

/// <summary>
/// Top-K 容器（<see cref="TopKResultStore"/>）维护策略测试。
///
/// 维护策略说明：
/// - 列表始终按指标从高到低排序；
/// - 未满 K 个时直接加入；
/// - 已满 K 个时，仅当新候选指标严格大于第 K 名（列表末尾）才淘汰第 K 名；
/// - 相同指标时保留先发现的候选（不替换），保证结果稳定。
/// </summary>
public sealed class TopKResultStoreTests
{
    private static TopKResult Make(string id, int metric, ScanMode mode = ScanMode.MaxGap) => new()
    {
        Id = id,
        Mode = mode,
        KeyMetric = metric,
        HundredCount = metric > 0 ? 2 : 0
    };

    [Fact]
    public void TryAdd_KeepsTop3ByMetric()
    {
        var store = new TopKResultStore(3, ScanMode.MaxGap);

        Assert.True(store.TryAdd(Make("a", 10)));
        Assert.True(store.TryAdd(Make("b", 30)));
        Assert.True(store.TryAdd(Make("c", 20)));
        // 已满 K=3：以下两个指标超过当前第 3 名（10），应淘汰它。
        Assert.True(store.TryAdd(Make("d", 50)));
        Assert.True(store.TryAdd(Make("e", 40)));

        var ranked = store.GetRanked();
        Assert.Equal(3, ranked.Count);
        Assert.Equal(new List<string> { "d", "e", "b" }, ranked.Select(x => x.Id).ToList());
        Assert.Equal(new List<int> { 50, 40, 30 }, ranked.Select(x => x.KeyMetric).ToList());
    }

    [Fact]
    public void TryAdd_RejectsWhenMetricNotAboveKth()
    {
        var store = new TopKResultStore(3, ScanMode.MaxGap);
        store.TryAdd(Make("a", 10));
        store.TryAdd(Make("b", 30));
        store.TryAdd(Make("c", 20));
        // 当前 Top-3: [30, 20, 10]，第 3 名（最小指标）= 10。

        // 指标 5 不超过第 3 名 10，拒绝。
        Assert.False(store.TryAdd(Make("d", 5)));
        // 指标 20 超过第 3 名 10，进入并淘汰 10。
        Assert.True(store.TryAdd(Make("e", 20)));
        // 淘汰后 Top-3: [30, 20, 20]，第 3 名变为 20；指标 19 不再超过，拒绝。
        Assert.False(store.TryAdd(Make("f", 19)));

        var ranked = store.GetRanked();
        Assert.Equal(3, ranked.Count);
        Assert.Equal(new List<int> { 30, 20, 20 }, ranked.Select(x => x.KeyMetric).ToList());
        // 同指标 20 时保留先发现的 c（发现序号更小）。
        Assert.Equal(new List<string> { "b", "c", "e" }, ranked.Select(x => x.Id).ToList());
    }

    [Fact]
    public void TryAdd_KeepsAllWhenUnderCapacity()
    {
        var store = new TopKResultStore(5, ScanMode.MaxGap);
        Assert.True(store.TryAdd(Make("a", 10)));
        Assert.True(store.TryAdd(Make("b", 20)));

        Assert.Equal(2, store.GetRanked().Count);
        // 未满 K 时不存在“第 K 名”，CurrentMinMetric 应为 null。
        Assert.Null(store.CurrentMinMetric);
    }

    [Fact]
    public void TryAdd_TieKeepsFirstDiscovered()
    {
        var store = new TopKResultStore(1, ScanMode.MaxGap);
        Assert.True(store.TryAdd(Make("a", 30)));

        // 相同指标不替换：b 无法挤掉 a。
        Assert.False(store.TryAdd(Make("b", 30)));

        var best = store.Best;
        Assert.NotNull(best);
        Assert.Equal("a", best.Id);
    }

    [Fact]
    public void CurrentMinMetric_ReflectsKthMetric()
    {
        var store = new TopKResultStore(3, ScanMode.MaxGap);
        store.TryAdd(Make("a", 10));
        Assert.Null(store.CurrentMinMetric);

        store.TryAdd(Make("b", 30));
        Assert.Null(store.CurrentMinMetric);

        store.TryAdd(Make("c", 20));
        Assert.Equal(10, store.CurrentMinMetric); // 满 K，第 3 名 = 10

        store.TryAdd(Make("d", 50));
        Assert.Equal(20, store.CurrentMinMetric); // 淘汰 10 后，第 3 名 = 20
    }

    [Fact]
    public void TryAdd_RejectsCandidateWithDifferentMode()
    {
        var store = new TopKResultStore(3, ScanMode.MaxGap);
        Assert.Throws<ArgumentException>(
            () => store.TryAdd(Make("x", 10, ScanMode.First100Date)));
    }

    [Fact]
    public void GetRanked_SortsByMetricDescThenDiscovery()
    {
        var store = new TopKResultStore(10, ScanMode.MaxGap);
        store.TryAdd(Make("a", 5));
        store.TryAdd(Make("b", 50));
        store.TryAdd(Make("c", 15));
        store.TryAdd(Make("d", 50)); // 与 b 同指标，b 先发现应排前

        var ranked = store.GetRanked();
        Assert.Equal(new List<string> { "b", "d", "c", "a" }, ranked.Select(x => x.Id).ToList());
    }

    [Fact]
    public void GetDiscoveryOrdered_ReflectsInsertionOrder()
    {
        var store = new TopKResultStore(10, ScanMode.MaxGap);
        store.TryAdd(Make("a", 5));
        store.TryAdd(Make("b", 50));
        store.TryAdd(Make("c", 15));

        // UI 下拉列表按“发现时间”（进入 Top-K 的先后）排序。
        var ordered = store.GetDiscoveryOrdered();
        Assert.Equal(new List<string> { "a", "b", "c" }, ordered.Select(x => x.Id).ToList());

        // 发现序号必须单调递增。
        var sequences = ordered.Select(x => x.DiscoveredAt).ToArray();
        Assert.Equal(sequences.OrderBy(x => x).ToArray(), sequences);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveK()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TopKResultStore(0, ScanMode.MaxGap));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TopKResultStore(-1, ScanMode.MaxGap));
    }
}
