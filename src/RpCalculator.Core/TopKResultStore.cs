namespace RpCalculator.Core;

/// <summary>
/// 线程安全的 Top-K 结果容器。
///
/// 内部使用 List 模拟小顶堆的“淘汰”语义：
/// - 列表始终按指标从高到低排序；
/// - 当未满 K 个时直接加入；
/// - 当已满 K 个时，如果新候选的指标大于当前第 K 名（列表末尾），则淘汰第 K 名并加入新候选。
///
/// 由于 K 上限为 1000，List 的 O(K) 插入成本可忽略，而且比手写堆更简单、更不易出错。
/// 所有修改和读取都通过 lock 保护，保证并行环境安全。
/// 每个候选进入 Top-K 时分配单调递增的 DiscoveredAt，UI 可据此按“发现时间”排序。
/// </summary>
public sealed class TopKResultStore
{
    private readonly object _gate = new();
    private readonly List<TopKResult> _items = new();
    private readonly int _k;
    private readonly ScanMode _mode;
    private long _sequence;

    public TopKResultStore(int k, ScanMode mode)
    {
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "K 必须大于 0。");
        }

        _k = k;
        _mode = mode;
    }

    public int K => _k;

    public ScanMode Mode => _mode;

    /// <summary>
    /// 当前 Top-K 中第 K 名的指标值；未满 K 个时为 null。
    /// 调用方可用它做无锁快速预检，避免不必要的完整扫描。
    /// </summary>
    public int? CurrentMinMetric
    {
        get
        {
            lock (_gate)
            {
                return _items.Count < _k ? null : _items[^1].KeyMetric;
            }
        }
    }

    /// <summary>
    /// 尝试加入一个候选。返回 true 表示该候选进入了 Top-K。
    /// 相同指标时保留先发现的候选（不替换），使结果更稳定。
    /// </summary>
    public bool TryAdd(TopKResult candidate)
    {
        if (candidate.Mode != _mode)
        {
            throw new ArgumentException("候选结果模式与 Top-K 容器模式不一致。", nameof(candidate));
        }

        lock (_gate)
        {
            if (_items.Count < _k)
            {
                var added = CopyWithSequence(candidate, _sequence++);
                _items.Add(added);
                SortDescending();
                return true;
            }

            var worst = _items[^1];
            if (candidate.KeyMetric > worst.KeyMetric)
            {
                _items.RemoveAt(_items.Count - 1);
                var added = CopyWithSequence(candidate, _sequence++);
                _items.Add(added);
                SortDescending();
                return true;
            }

            return false;
        }
    }

    /// <summary>返回按指标从高到低排列的快照（同指标按发现时间先后）。</summary>
    public IReadOnlyList<TopKResult> GetRanked()
    {
        lock (_gate)
        {
            return _items
                .OrderByDescending(x => x.KeyMetric)
                .ThenBy(x => x.DiscoveredAt)
                .ToArray();
        }
    }

    /// <summary>返回按发现时间从早到晚排列的快照，供 UI 下拉列表使用。</summary>
    public IReadOnlyList<TopKResult> GetDiscoveryOrdered()
    {
        lock (_gate)
        {
            return _items
                .OrderBy(x => x.DiscoveredAt)
                .ToArray();
        }
    }

    /// <summary>当前最佳（指标最高）。</summary>
    public TopKResult? Best
    {
        get
        {
            var ranked = GetRanked();
            return ranked.Count > 0 ? ranked[0] : null;
        }
    }

    private void SortDescending()
    {
        _items.Sort((a, b) =>
        {
            var byMetric = b.KeyMetric.CompareTo(a.KeyMetric);
            return byMetric != 0 ? byMetric : a.DiscoveredAt.CompareTo(b.DiscoveredAt);
        });
    }

    private static TopKResult CopyWithSequence(TopKResult source, long discoveredAt)
    {
        return new TopKResult
        {
            Id = source.Id,
            Mode = source.Mode,
            KeyMetric = source.KeyMetric,
            HundredCount = source.HundredCount,
            HundredDates = source.HundredDates,
            First100DateIndex = source.First100DateIndex,
            First100Date = source.First100Date,
            DiscoveredAt = discoveredAt
        };
    }
}
