using RpCalculator.Core;

namespace RpCalculator.Core.Tests;

/// <summary>
/// 并行处理器测试。
///
/// 关键点：并行 Top-K 的“发现时间”（DiscoveredAt）取决于线程调度，无法与单线程完全一致；
/// 但 Top-K 的“指标值序列”（降序）只由候选的指标多重集合决定，与处理顺序无关，
/// 因此所有一致性断言都以指标序列为准，避免 flaky。
/// </summary>
public sealed class ParallelProcessorTests
{
    [Fact]
    public async Task ProcessAsync_MaxGap_MatchesSequentialBest()
    {
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 400);
        var source = new RandomIdGenerator(12345);
        var ids = source.Take(1000).ToList();

        var sequentialBest = ComputeSequentialBest(ids, info);
        Assert.NotNull(sequentialBest);

        var result = await ParallelRpProcessor.ProcessAsync(
            ids,
            info,
            totalCount: 1000,
            mode: ScanMode.MaxGap,
            k: 10,
            maxDegreeOfParallelism: 4,
            batchSize: 37);

        Assert.True(result.IsCompleted);
        Assert.False(result.IsCancelled);
        Assert.Equal(1000, result.ProcessedCount);

        Assert.NotNull(result.Best);
        Assert.Equal(sequentialBest.Id, result.Best.Id);
        Assert.Equal(sequentialBest.MaxGap, result.Best.KeyMetric);
        Assert.Equal(sequentialBest.HundredCount, result.Best.HundredCount);
        Assert.Equal(sequentialBest.HundredDates.Count, result.Best.HundredDates.Count);
        Assert.Equal(sequentialBest.HundredDates, result.Best.HundredDates);
    }

    [Fact]
    public async Task ProcessAsync_MaxGap_TopK_MatchesSequentialTopK()
    {
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 400);
        var ids = new RandomIdGenerator(12345).Take(1000).ToList();
        const int k = 10;

        var sequential = ComputeSequentialTopK(ids, info, k, ScanMode.MaxGap);
        var result = await ParallelRpProcessor.ProcessAsync(
            ids,
            info,
            totalCount: 1000,
            mode: ScanMode.MaxGap,
            k: k,
            maxDegreeOfParallelism: 4,
            batchSize: 37);

        // 并行与单线程的 Top-K 指标序列（降序）必须一致。
        Assert.Equal(
            sequential.Select(x => x.KeyMetric).ToArray(),
            result.TopResults.Select(x => x.KeyMetric).ToArray());

        // 并行结果中每个候选都与它自己的完整扫描结果一致（构造无 bug）。
        foreach (var item in result.TopResults)
        {
            var full = RpScanner.ScanWithDates(item.Id, info);
            Assert.Equal(full.MaxGap, item.KeyMetric);
            Assert.Equal(full.HundredCount, item.HundredCount);
            Assert.Equal(full.HundredDates, item.HundredDates);
        }
    }

    [Fact]
    public async Task ProcessAsync_First100_MatchesSequentialTopK()
    {
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 400);
        var ids = new RandomIdGenerator(12345).Take(1000).ToList();
        const int k = 10;

        var sequential = ComputeSequentialTopK(ids, info, k, ScanMode.First100Date);
        var result = await ParallelRpProcessor.ProcessAsync(
            ids,
            info,
            totalCount: 1000,
            mode: ScanMode.First100Date,
            k: k,
            maxDegreeOfParallelism: 4,
            batchSize: 37);

        Assert.True(result.IsCompleted);
        Assert.Equal(1000, result.ProcessedCount);

        Assert.Equal(
            sequential.Select(x => x.KeyMetric).ToArray(),
            result.TopResults.Select(x => x.KeyMetric).ToArray());

        // 距今最久模式的每个候选应保留其第一个 100 分日期与索引，且与单扫一致。
        foreach (var item in result.TopResults)
        {
            var single = RpScanner.ScanFirst100(item.Id, info);
            Assert.True(single.Found);
            Assert.Equal(single.DateIndex, item.KeyMetric);
            Assert.Equal(single.DateIndex, item.First100DateIndex);
            Assert.Equal(single.Date, item.First100Date);
        }
    }

    [Fact]
    public async Task ProcessAsync_First100_ExcludesIdsWithoutHundred()
    {
        // 在 400 天窗口内，绝大多数字符串都会有 100 分日期；
        // 使用“test”等少量 id 且窗口很短时，可构造出没有 100 分的输入，
        // 验证这些 id 不会进入 Top-K。
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 1);
        var noHundredIds = new List<string>();
        foreach (var id in new[] { "test", "xyz", "nope123" })
        {
            if (!RpScanner.ScanFirst100(id, info).Found)
            {
                noHundredIds.Add(id);
            }
        }

        if (noHundredIds.Count == 0)
        {
            return; // 该窗口下没有可用的“无 100 分”样本，跳过。
        }

        // 混合一批必然有效的 id，确保整体仍有结果。
        var mixed = noHundredIds.Concat(["abc"]).ToArray();
        var result = await ParallelRpProcessor.ProcessAsync(
            mixed,
            info,
            totalCount: mixed.Length,
            mode: ScanMode.First100Date,
            k: 10,
            maxDegreeOfParallelism: 2,
            batchSize: 10);

        foreach (var id in noHundredIds)
        {
            Assert.DoesNotContain(result.TopResults, x => x.Id == id);
        }
    }

    [Fact]
    public async Task ProcessAsync_ImmediateCancellation_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 30);
        var result = await ParallelRpProcessor.ProcessAsync(
            new[] { "abc" },
            info,
            totalCount: 1,
            mode: ScanMode.MaxGap,
            k: 10,
            cancellationToken: cts.Token,
            batchSize: 10,
            maxDegreeOfParallelism: 2);

        Assert.True(result.IsCancelled);
        Assert.False(result.IsCompleted);
        Assert.Equal(0, result.ProcessedCount);
    }

    [Fact]
    public async Task ProcessAsync_HandlesEmptySource()
    {
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 100);
        var result = await ParallelRpProcessor.ProcessAsync(
            Array.Empty<string>(),
            info,
            totalCount: 0,
            mode: ScanMode.MaxGap,
            k: 10,
            batchSize: 10);

        Assert.True(result.IsCompleted);
        Assert.Equal(0, result.ProcessedCount);
        Assert.Null(result.Best);
        Assert.Empty(result.TopResults);
    }

    [Fact]
    public async Task ProcessLargeStream_CompletesWithBoundedBatches()
    {
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 1780);
        var source = new RandomIdGenerator(12345);
        var ids = source.Take(50_000).ToList();

        // 该测试验证流式 + 分批处理能稳定完成，并且内存不会无界增长。
        // 阈值给得很宽，避免 CI 抖动；若实现误把全部识别码或每日结果缓存起来，会明显超过此值。
        var before = GC.GetTotalMemory(forceFullCollection: true);

        var result = await ParallelRpProcessor.ProcessAsync(
            ids,
            info,
            totalCount: 50_000,
            mode: ScanMode.MaxGap,
            k: 10,
            batchSize: 10_000,
            maxDegreeOfParallelism: 4);

        var after = GC.GetTotalMemory(forceFullCollection: true);

        Assert.True(result.IsCompleted);
        Assert.Equal(50_000, result.ProcessedCount);
        Assert.True(after < before + 512L * 1024 * 1024, "处理大量识别码时内存不应无界增长。");
    }

    [Fact]
    public async Task ProcessAsync_WithNormalizer_SkipsInvalidAndCounts()
    {
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 100);
        // 有效 2 个（ABCD-... 原样有效；abcd-... 规范化后有效），
        // 无效 4 个（首字符 0 / 非法字符 / 空行 / 纯空白）。
        string[] raw =
        [
            "ABCD-1234-5678-90EF",
            "0123-4567-890A-BCDE",   // 首字符 0 → 无效
            "bad!id",                // 非法字符 → 无效
            "",                      // 空行 → 无效
            "  ",                    // 纯空白 → 无效
            "abcd-ef01-2345-6789"    // 小写 → 规范化后有效
        ];

        var result = await ParallelRpProcessor.ProcessAsync(
            raw,
            info,
            totalCount: raw.Length,
            mode: ScanMode.MaxGap,
            k: 3,
            idNormalizer: line => IdFormat.TryNormalize(line, out var id) ? id : null,
            batchSize: 10,
            maxDegreeOfParallelism: 2);

        // 进度基于实际处理的识别码数量（有效 + 无效）。
        Assert.Equal(raw.Length, result.ProcessedCount);
        Assert.Equal(4, result.InvalidCount);

        // 进入 Top-K 的识别码必须全部有效。
        Assert.All(result.TopResults, item => Assert.True(IdFormat.IsValidId(item.Id)));
    }

    [Fact]
    public async Task ProcessAsync_AllInvalid_ReturnsEmptyTopK()
    {
        var info = new DateRangeInfo(new DateTime(2024, 1, 1), 30);
        string[] raw =
        [
            "0123-4567-890A-BCDE",
            "0000-0000-1234-5678",
            "xyz",
            "  "
        ];

        var result = await ParallelRpProcessor.ProcessAsync(
            raw,
            info,
            totalCount: raw.Length,
            mode: ScanMode.MaxGap,
            k: 3,
            idNormalizer: line => IdFormat.TryNormalize(line, out var id) ? id : null,
            batchSize: 10,
            maxDegreeOfParallelism: 2);

        Assert.Equal(raw.Length, result.ProcessedCount);
        Assert.Equal(raw.Length, result.InvalidCount);
        Assert.Empty(result.TopResults);
        Assert.Null(result.Best);
    }

    /// <summary>单线程“最大间隔”最佳（兼容旧测试语义）。</summary>
    private static BestResult? ComputeSequentialBest(IEnumerable<string> ids, DateRangeInfo info)
    {
        BestResult? best = null;

        foreach (var id in ids)
        {
            var full = RpScanner.ScanWithDates(id, info);
            if (best is null || full.MaxGap > best.MaxGap)
            {
                best = new BestResult
                {
                    Id = full.Id,
                    MaxGap = full.MaxGap,
                    HundredCount = full.HundredCount,
                    HundredDates = full.HundredDates
                };
            }
        }

        return best;
    }

    /// <summary>
    /// 单线程 Top-K 参考实现：与并行实现使用相同的 <see cref="TopKResultStore"/> 淘汰语义
    /// （相同指标保留先发现），按输入顺序逐个处理。返回按指标降序排列的结果。
    /// </summary>
    private static List<TopKResult> ComputeSequentialTopK(
        IEnumerable<string> ids,
        DateRangeInfo info,
        int k,
        ScanMode mode)
    {
        var store = new TopKResultStore(k, mode);

        foreach (var id in ids)
        {
            if (mode == ScanMode.MaxGap)
            {
                var full = RpScanner.ScanWithDates(id, info);
                if (full.MaxGap <= 0)
                {
                    continue; // 不足 2 个 100 分日期，无效。
                }

                store.TryAdd(new TopKResult
                {
                    Id = id,
                    Mode = mode,
                    KeyMetric = full.MaxGap,
                    HundredCount = full.HundredCount,
                    HundredDates = full.HundredDates
                });
            }
            else
            {
                var first = RpScanner.ScanFirst100(id, info);
                if (!first.Found)
                {
                    continue; // 窗口内无 100 分日期，无效。
                }

                store.TryAdd(new TopKResult
                {
                    Id = id,
                    Mode = mode,
                    KeyMetric = first.DateIndex,
                    HundredCount = 1,
                    HundredDates = new[] { first.Date },
                    First100DateIndex = first.DateIndex,
                    First100Date = first.Date
                });
            }
        }

        return store.GetRanked().ToList();
    }
}
