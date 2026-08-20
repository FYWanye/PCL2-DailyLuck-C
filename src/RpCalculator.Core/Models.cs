namespace RpCalculator.Core;

/// <summary>算法模式。</summary>
public enum ScanMode
{
    /// <summary>最大间隔：相邻两个 100 分日期的最大间隔天数。</summary>
    MaxGap,

    /// <summary>距今最久：第一个 100 分日期距离窗口起始日的天数（越大越好）。</summary>
    First100Date
}

/// <summary>单个识别码“最大间隔”模式的标量扫描结果（不保留日期列表）。</summary>
public readonly record struct RpCoreScanResult(int MaxGap, int HundredCount);

/// <summary>单个识别码“距今最久”模式的标量扫描结果。</summary>
public readonly record struct RpFirst100ScanResult(bool Found, int DateIndex, DateTime Date);

/// <summary>单个识别码的完整扫描结果，包含 100 分日期列表。</summary>
public sealed class RpScanResult
{
    public required string Id { get; init; }

    public int MaxGap { get; init; }

    public int HundredCount { get; init; }

    public IReadOnlyList<DateTime> HundredDates { get; init; } = Array.Empty<DateTime>();
}

/// <summary>
/// Top-K 中的一个结果项。
///
/// KeyMetric 在两种模式下的含义：
/// - MaxGap：最大间隔天数
/// - First100Date：第一个 100 分日期在窗口内的索引（第几天）
/// DiscoveredAt 用于 UI 按“发现时间”排序，由 TopKResultStore 分配。
/// </summary>
public sealed class TopKResult
{
    public required string Id { get; init; }

    public ScanMode Mode { get; init; }

    public int KeyMetric { get; init; }

    public int HundredCount { get; init; }

    public IReadOnlyList<DateTime> HundredDates { get; init; } = Array.Empty<DateTime>();

    /// <summary>仅“距今最久”模式使用；未找到时为 -1。</summary>
    public int First100DateIndex { get; init; } = -1;

    public DateTime? First100Date { get; init; }

    /// <summary>进入 Top-K 的单调递增序号，用于 UI 发现时间排序。</summary>
    public long DiscoveredAt { get; init; }

    public string DisplayText => Mode == ScanMode.MaxGap
        ? $"{Id}：最大间隔 {KeyMetric} 天"
        : $"{Id}：首次100分第 {KeyMetric} 天";
}

/// <summary>全局最佳结果（保留用于兼容旧代码；新逻辑请使用 TopKResult）。</summary>
public sealed class BestResult
{
    public string Id { get; init; } = string.Empty;

    public int MaxGap { get; init; }

    public int HundredCount { get; init; }

    public IReadOnlyList<DateTime> HundredDates { get; init; } = Array.Empty<DateTime>();

    public bool HasResult => MaxGap > 0;
}

/// <summary>进度通知负载。</summary>
public sealed class RpProgressInfo
{
    public long ProcessedCount { get; init; }

    public long? TotalCount { get; init; }

    public string CurrentBestId { get; init; } = string.Empty;

    /// <summary>当前最佳指标：最大间隔天数或第一个 100 分日期索引。</summary>
    public int CurrentBestMetric { get; init; }

    public int CurrentBestHundredCount { get; init; }

    /// <summary>累计被判定为无效并跳过的识别码数量（进度更新时反馈）。</summary>
    public long InvalidCount { get; init; }

    /// <summary>当前批次是否已经结束（完成或取消）。</summary>
    public bool IsFinal { get; init; }
}

/// <summary>一次完整扫描的最终结果。</summary>
public sealed class RpProcessingResult
{
    /// <summary>Top-K 结果，按指标从高到低排列。</summary>
    public IReadOnlyList<TopKResult> TopResults { get; init; } = Array.Empty<TopKResult>();

    public long ProcessedCount { get; init; }

    /// <summary>被判定为无效并跳过的识别码数量（不参与排名）。</summary>
    public long InvalidCount { get; init; }

    public bool IsCompleted { get; init; }

    public bool IsCancelled { get; init; }

    public TimeSpan Elapsed { get; init; }

    /// <summary>当前排名第一的结果；没有有效结果时为 null。</summary>
    public TopKResult? Best => TopResults.Count > 0 ? TopResults[0] : null;
}
