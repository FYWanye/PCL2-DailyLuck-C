using System.Diagnostics;

namespace RpCalculator.Core;

/// <summary>
/// 并行处理识别码流，维护 Top-K 最佳结果。
///
/// 设计要点：
/// 1. 识别码源是惰性流（IEnumerable），每次只物化一个批次，不会一次性加载全部。
/// 2. Parallel.ForEach 每个 worker 维护自己的“局部 Top-K”（最多 K 个候选的标量信息），
///    批处理结束后合并，减少锁竞争。
///    —— 为什么是“局部 Top-K”而不是“单一局部最佳”？
///    单一最佳会丢失“与局部最佳不同指标、但也能进全局 Top-K”的候选
///    （例如局部最佳 45、另一候选 43，而全局第 K 名是 42），导致并行结果与单线程不一致。
///    局部 Top-K 保证每个候选都有机会参与全局淘汰，结果与单线程严格一致。
/// 3. 只有可能进入全局 Top-K 的候选，才在“最大间隔”模式下做第二次完整扫描
///    收集 100 分日期；绝大多数候选只走 O(1) 内存的标量扫描。
/// 4. “距今最久”模式使用早停扫描：每个识别码找到第一个 100 分日期后立即返回，
///    不再计算后续日期，也绝不计算最大间隔。
/// 5. TopKResultStore 内部用 lock 保护，所有读取/写入线程安全。
/// </summary>
public static class ParallelRpProcessor
{
    public static Task<RpProcessingResult> ProcessAsync(
        IEnumerable<string> ids,
        DateRangeInfo dateRange,
        long? totalCount,
        ScanMode mode,
        int k,
        IProgress<RpProgressInfo>? progress = null,
        CancellationToken cancellationToken = default,
        Func<string, string?>? idNormalizer = null,
        int batchSize = 100_000,
        int maxDegreeOfParallelism = 0)
    {
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "K 必须大于 0。");
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "批次大小必须大于 0。");
        }

        if (maxDegreeOfParallelism <= 0)
        {
            maxDegreeOfParallelism = Environment.ProcessorCount;
        }

        // CPU 密集计算放到线程池，避免占用 UI 线程。
        // 不把 cancellationToken 传给 Task.Run：若任务尚未开始就已取消，
        // 我们仍希望返回“已取消但带当前最佳”的结果，而不是直接抛出 OCE。
        return Task.Run(
            () => ProcessCore(
                ids,
                dateRange,
                totalCount,
                mode,
                k,
                progress,
                cancellationToken,
                idNormalizer,
                batchSize,
                maxDegreeOfParallelism));
    }

    private static RpProcessingResult ProcessCore(
        IEnumerable<string> ids,
        DateRangeInfo dateRange,
        long? totalCount,
        ScanMode mode,
        int k,
        IProgress<RpProgressInfo>? progress,
        CancellationToken cancellationToken,
        Func<string, string?>? idNormalizer,
        int batchSize,
        int maxDegreeOfParallelism)
    {
        var stopwatch = Stopwatch.StartNew();
        var store = new TopKResultStore(k, mode);
        long processedCount = 0;
        long invalidCount = 0;
        var isCancelled = false;

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = maxDegreeOfParallelism
        };

        using var enumerator = ids.GetEnumerator();

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 只物化一个批次。100,000 个 string 引用约 0.8 MB，内存可控。
                var batch = new List<string>(batchSize);
                while (batch.Count < batchSize && enumerator.MoveNext())
                {
                    batch.Add(enumerator.Current);
                }

                if (batch.Count == 0)
                {
                    break;
                }

                Parallel.ForEach(
                    batch,
                    parallelOptions,
                    () => new LocalBest(k, mode),
                    (id, _, local) =>
                    {
                        // 快速取消检查。ParallelOptions 也会在协作点抛出 OCE。
                        cancellationToken.ThrowIfCancellationRequested();

                        // 规范化识别码：null 表示无效（如文件行格式错误/首字符为 0）。
                        // 无效识别码计入处理数量与无效计数，但绝不参与排名。
                        // 注意：不能用 ?? id 回退——normalizer 返回 null 恰恰表示“无效”，
                        // 回退成原始行会导致无效识别码混入计算。
                        var current = idNormalizer is null ? id : idNormalizer(id);
                        if (current is null)
                        {
                            local.Processed++;
                            local.Invalid++;
                            return local;
                        }

                        if (mode == ScanMode.MaxGap)
                        {
                            var scan = RpScanner.ScanCore(current, dateRange);
                            // 指标 0 表示不足 2 个 100 分日期，无效，不进入局部 Top-K。
                            if (scan.MaxGap > 0)
                            {
                                local.TryAdd(new LocalEntry(current, scan.MaxGap, scan.HundredCount, -1, default));
                            }
                        }
                        else
                        {
                            var scan = RpScanner.ScanFirst100(current, dateRange);
                            // 早停：找到第一个 100 分立即返回，无 100 分则 Found=false，无效。
                            if (scan.Found)
                            {
                                local.TryAdd(new LocalEntry(current, scan.DateIndex, 1, scan.DateIndex, scan.Date));
                            }
                        }

                        local.Processed++;
                        return local;
                    },
                    local =>
                    {
                        Interlocked.Add(ref processedCount, local.Processed);
                        if (local.Invalid > 0)
                        {
                            Interlocked.Add(ref invalidCount, local.Invalid);
                        }

                        TryMergeLocalBest(local, dateRange, store);
                    });

                progress?.Report(CreateProgress(processedCount, invalidCount, totalCount, store));
            }

            isCancelled = cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            isCancelled = true;
        }

        progress?.Report(CreateProgress(processedCount, invalidCount, totalCount, store, isFinal: true));

        return new RpProcessingResult
        {
            TopResults = store.GetRanked(),
            ProcessedCount = Volatile.Read(ref processedCount),
            InvalidCount = Volatile.Read(ref invalidCount),
            IsCompleted = !isCancelled,
            IsCancelled = isCancelled,
            Elapsed = stopwatch.Elapsed
        };
    }

    /// <summary>
    /// 把 worker 的局部 Top-K 全部合并进全局 Top-K。
    /// 局部列表已按指标降序排列，先合并高指标的候选。
    /// </summary>
    private static void TryMergeLocalBest(LocalBest local, DateRangeInfo dateRange, TopKResultStore store)
    {
        foreach (var entry in local.Entries)
        {
            TryMergeCandidate(entry, dateRange, store);
        }
    }

    private static void TryMergeCandidate(LocalEntry entry, DateRangeInfo dateRange, TopKResultStore store)
    {
        // MaxGap 模式：指标 0 表示不足 2 个 100 分日期，无效（局部加入时已过滤，此处防御）。
        if (store.Mode == ScanMode.MaxGap && entry.KeyMetric <= 0)
        {
            return;
        }

        // First100 模式：-1 表示没有 100 分日期，无效。
        if (store.Mode == ScanMode.First100Date && entry.KeyMetric < 0)
        {
            return;
        }

        // 无锁快速预检：如果连当前第 K 名都超不过，就不需要构造候选/完整扫描。
        var minMetric = store.CurrentMinMetric;
        if (minMetric.HasValue && entry.KeyMetric <= minMetric.Value)
        {
            return;
        }

        if (store.Mode == ScanMode.MaxGap)
        {
            // 只有真正可能进入 Top-K 的候选，才收集 100 分日期列表。
            // 这类候选数量很少（最多每个 worker 的局部 Top-K），二次扫描的开销可以接受。
            var full = RpScanner.ScanWithDates(entry.Id, dateRange);
            var candidate = new TopKResult
            {
                Id = full.Id,
                Mode = ScanMode.MaxGap,
                KeyMetric = full.MaxGap,
                HundredCount = full.HundredCount,
                HundredDates = full.HundredDates
            };

            store.TryAdd(candidate);
        }
        else
        {
            // 距今最久模式：早停扫描已经拿到第一个 100 分日期，不需要二次完整扫描。
            var candidate = new TopKResult
            {
                Id = entry.Id,
                Mode = ScanMode.First100Date,
                KeyMetric = entry.KeyMetric,
                HundredCount = 1,
                HundredDates = new[] { entry.First100Date },
                First100DateIndex = entry.First100DateIndex,
                First100Date = entry.First100Date
            };

            store.TryAdd(candidate);
        }
    }

    private static RpProgressInfo CreateProgress(
        long processedCount,
        long invalidCount,
        long? totalCount,
        TopKResultStore store,
        bool isFinal = false)
    {
        var best = store.Best;
        return new RpProgressInfo
        {
            ProcessedCount = processedCount,
            InvalidCount = invalidCount,
            TotalCount = totalCount,
            CurrentBestId = best?.Id ?? string.Empty,
            CurrentBestMetric = best?.KeyMetric ?? 0,
            CurrentBestHundredCount = best?.HundredCount ?? 0,
            IsFinal = isFinal
        };
    }

    /// <summary>
    /// worker 局部 Top-K：只保留标量信息，不保存每日结果，内存 O(K) 每 worker。
    /// 维护策略与全局 TopKResultStore 一致：按指标降序、同指标保留先发现、
    /// 已满 K 时仅当新指标严格大于第 K 名才淘汰。
    /// </summary>
    private sealed class LocalBest
    {
        private readonly int _k;
        private readonly List<LocalEntry> _entries = new();

        public LocalBest(int k, ScanMode mode)
        {
            _k = k;
            _ = mode; // 无效指标过滤已在扫描分支完成。
        }

        /// <summary>本 worker 实际消费的识别码总数（有效 + 无效）。</summary>
        public long Processed;

        /// <summary>本 worker 判定为无效的识别码数量。</summary>
        public long Invalid;

        /// <summary>按指标降序排列的局部 Top-K 快照（同指标按发现先后）。</summary>
        public IReadOnlyList<LocalEntry> Entries => _entries;

        /// <summary>
        /// 尝试把新候选加入局部 Top-K，返回是否进入。
        /// 列表始终按指标降序且同指标稳定（先发现在前），
        /// 已满 K 时只有指标严格大于第 K 名才淘汰，与全局容器语义一致。
        /// </summary>
        public bool TryAdd(LocalEntry candidate)
        {
            // 从末尾向前找第一个指标 >= 候选的元素，插入到它之后，
            // 这样同指标的新候选总是排在已有候选后面（稳定，保留先发现）。
            var insertAt = _entries.Count;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].KeyMetric >= candidate.KeyMetric)
                {
                    insertAt = i + 1;
                    break;
                }
            }

            if (_entries.Count < _k)
            {
                _entries.Insert(insertAt, candidate);
                return true;
            }

            // 已满 K：仅当插入位置在 K 之前（即优于第 K 名）才淘汰第 K 名。
            if (insertAt < _k)
            {
                _entries.RemoveAt(_entries.Count - 1);
                _entries.Insert(insertAt, candidate);
                return true;
            }

            return false;
        }
    }

    /// <summary>局部 Top-K 条目的标量信息。</summary>
    private readonly record struct LocalEntry(
        string Id,
        int KeyMetric,
        int HundredCount,
        int First100DateIndex,
        DateTime First100Date);
}
