using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RpCalculator.App.Mvvm;
using RpCalculator.Core;

namespace RpCalculator.App;

public enum SourceMode
{
    Random,
    File
}

public sealed record ModeOption(ScanMode Mode, string DisplayName);

public sealed class MainViewModel : ObservableObject
{
    private const long MaxCount = 10_000_000_000L;
    private const int MaxWindowDays = 100_000;
    private const int DefaultBatchSize = 100_000;
    private const int MinK = 1;
    private const int MaxK = 1000;

    private readonly ObservableCollection<string> _hundredDates = new();
    private readonly ObservableCollection<TopKResult> _topResults = new();

    private string _countText = "1000000";
    private string _daysText = "1780";
    private DateTime _startDate = DateTime.Today;
    private SourceMode _sourceMode = SourceMode.Random;
    private string _filePath = string.Empty;
    private string _kText = "10";
    private ModeOption _selectedModeOption = new(ScanMode.MaxGap, "最大间隔");

    private bool _isBusy;
    private bool _isIndeterminate;
    private double _progressMaximum = 100;
    private double _progressValue;
    private string _statusText = "就绪。";
    private string _processedText = string.Empty;
    private string _invalidCountText = string.Empty;
    private string _currentBestText = "暂无";
    private string _elapsedText = string.Empty;
    private string _resultId = string.Empty;
    private string _resultMetricText = "0";
    private string _resultHundredCountText = "0";
    private string _resultFirstDateText = string.Empty;
    private bool _hasResult;
    private TopKResult? _selectedResult;

    // 原始计算（独立验算）相关状态。
    private string _rawIdInput = string.Empty;
    private string _rawDaysText = "1780";
    private DateTime _rawStartDate = DateTime.Today;
    private bool _rawBusy;
    private string _rawStatusText = "输入识别码并点击“原始计算”以独立验算。";
    private string _rawResultSummary = string.Empty;
    private string _rawMaxGapText = "—";
    private string _rawHundredCountText = "—";
    private readonly ObservableCollection<string> _rawHundredDates = new();

    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        Modes =
        [
            new ModeOption(ScanMode.MaxGap, "最大间隔"),
            new ModeOption(ScanMode.First100Date, "距今最久")
        ];

        StartCommand = new AsyncRelayCommand(_ => StartAsync(), _ => !IsBusy);
        CancelCommand = new RelayCommand(_ => Cancel(), _ => IsBusy);
        BrowseFileCommand = new RelayCommand(_ => BrowseFile());
        ToggleThemeCommand = new RelayCommand(_ => ThemeManager.Toggle());
        CopyResultCommand = new AsyncRelayCommand(_ => CopyResultAsync(), _ => HasResult && !IsBusy);
        RawComputeCommand = new AsyncRelayCommand(_ => RawComputeAsync(), _ => !RawBusy && !IsBusy);
        FillRawFromBestCommand = new RelayCommand(_ => FillRawFromBest(), _ => SelectedResult is not null && !RawBusy && !IsBusy);
    }

    public ObservableCollection<string> HundredDates => _hundredDates;
    public ObservableCollection<TopKResult> TopResults => _topResults;
    public ObservableCollection<string> RawHundredDates => _rawHundredDates;

    public IReadOnlyList<ModeOption> Modes { get; }

    public string CountText
    {
        get => _countText;
        set => SetProperty(ref _countText, value);
    }

    public string DaysText
    {
        get => _daysText;
        set => SetProperty(ref _daysText, value);
    }

    public DateTime StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    public string KText
    {
        get => _kText;
        set
        {
            if (SetProperty(ref _kText, value) && HasResult)
            {
                StatusText = "K 值已更改，请重新计算。";
            }
        }
    }

    public ModeOption SelectedModeOption
    {
        get => _selectedModeOption;
        set
        {
            if (value is null || !SetProperty(ref _selectedModeOption, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedMode));
            OnPropertyChanged(nameof(ModeSubtitle));

            if (HasResult)
            {
                ResetResult();
                StatusText = "算法模式已更改，请重新计算。";
            }
        }
    }

    public ScanMode SelectedMode => SelectedModeOption.Mode;

    public string ModeSubtitle => SelectedMode == ScanMode.MaxGap
        ? "寻找 100 分日期最大间隔最大的识别码"
        : "寻找第一个 100 分日期出现最晚的识别码";

    public bool IsRandomSource
    {
        get => _sourceMode == SourceMode.Random;
        set
        {
            if (value)
            {
                CurrentSourceMode = SourceMode.Random;
            }
        }
    }

    public bool IsFileSource
    {
        get => _sourceMode == SourceMode.File;
        set
        {
            if (value)
            {
                CurrentSourceMode = SourceMode.File;
            }
        }
    }

    public SourceMode CurrentSourceMode
    {
        get => _sourceMode;
        private set
        {
            if (SetProperty(ref _sourceMode, value))
            {
                OnPropertyChanged(nameof(IsRandomSource));
                OnPropertyChanged(nameof(IsFileSource));
                OnPropertyChanged(nameof(FilePanelVisibility));
            }
        }
    }

    public Visibility FilePanelVisibility => IsFileSource ? Visibility.Visible : Visibility.Collapsed;

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                StartCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
                CopyResultCommand.RaiseCanExecuteChanged();
                RawComputeCommand.RaiseCanExecuteChanged();
                FillRawFromBestCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        private set => SetProperty(ref _isIndeterminate, value);
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        private set => SetProperty(ref _progressMaximum, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ProcessedText
    {
        get => _processedText;
        private set => SetProperty(ref _processedText, value);
    }

    /// <summary>无效识别码统计文案，如“共跳过无效识别码：123 个”。无跳过时为空。</summary>
    public string InvalidCountText
    {
        get => _invalidCountText;
        private set => SetProperty(ref _invalidCountText, value);
    }

    public string CurrentBestText
    {
        get => _currentBestText;
        private set => SetProperty(ref _currentBestText, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    public string ResultId
    {
        get => _resultId;
        private set => SetProperty(ref _resultId, value);
    }

    public string ResultMetricText
    {
        get => _resultMetricText;
        private set => SetProperty(ref _resultMetricText, value);
    }

    public string ResultHundredCountText
    {
        get => _resultHundredCountText;
        private set => SetProperty(ref _resultHundredCountText, value);
    }

    public string ResultFirstDateText
    {
        get => _resultFirstDateText;
        private set => SetProperty(ref _resultFirstDateText, value);
    }

    public bool HasResult
    {
        get => _hasResult;
        private set
        {
            if (SetProperty(ref _hasResult, value))
            {
                CopyResultCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public TopKResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetProperty(ref _selectedResult, value))
            {
                UpdateSelectedResultDetail();
                FillRawFromBestCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand BrowseFileCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public AsyncRelayCommand CopyResultCommand { get; }
    public AsyncRelayCommand RawComputeCommand { get; }
    public RelayCommand FillRawFromBestCommand { get; }

    // ==================== 原始计算（独立验算） ====================

    /// <summary>待验算的识别码（用户可手动输入，也可点击“填入最佳”从主结果复制）。</summary>
    public string RawIdInput
    {
        get => _rawIdInput;
        set => SetProperty(ref _rawIdInput, value);
    }

    /// <summary>原始计算窗口天数文本框。</summary>
    public string RawDaysText
    {
        get => _rawDaysText;
        set => SetProperty(ref _rawDaysText, value);
    }

    /// <summary>原始计算起始日期。</summary>
    public DateTime RawStartDate
    {
        get => _rawStartDate;
        set => SetProperty(ref _rawStartDate, value);
    }

    public bool RawBusy
    {
        get => _rawBusy;
        private set
        {
            if (SetProperty(ref _rawBusy, value))
            {
                RawComputeCommand.RaiseCanExecuteChanged();
                FillRawFromBestCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string RawStatusText
    {
        get => _rawStatusText;
        private set => SetProperty(ref _rawStatusText, value);
    }

    public string RawResultSummary
    {
        get => _rawResultSummary;
        private set => SetProperty(ref _rawResultSummary, value);
    }

    public string RawMaxGapText
    {
        get => _rawMaxGapText;
        private set => SetProperty(ref _rawMaxGapText, value);
    }

    public string RawHundredCountText
    {
        get => _rawHundredCountText;
        private set => SetProperty(ref _rawHundredCountText, value);
    }

    private async Task RawComputeAsync()
    {
        if (RawBusy || IsBusy)
        {
            return;
        }

        var idText = (RawIdInput ?? string.Empty).Trim();
        if (idText.Length == 0)
        {
            RawStatusText = "请先输入要验算的识别码。";
            return;
        }

        // 规范化识别码：去除短横线、统一大写，并按 4-4-4-4 输出。
        // 若用户输入了首字符为 '0' 的识别码也允许——验算功能不应阻拦边界用例。
        string canonicalId;
        try
        {
            canonicalId = CanonicalizeForRawVerify(idText);
        }
        catch (ArgumentException ex)
        {
            RawStatusText = ex.Message;
            return;
        }

        if (!int.TryParse(RawDaysText, out var days) || days <= 0 || days > MaxWindowDays)
        {
            RawStatusText = $"窗口天数必须介于 1 和 {MaxWindowDays:N0} 之间。";
            return;
        }

        var startDate = RawStartDate.Date;

        RawBusy = true;
        RawStatusText = "正在按原始算法逐日验算…";
        RawResultSummary = string.Empty;
        RawMaxGapText = "—";
        RawHundredCountText = "—";
        _rawHundredDates.Clear();

        // 故意把整个原始计算放到线程池：它的实现刻意没有做任何优化，
        // 对长窗口（数万天）会跑得比主扫描器慢很多，UI 线程必须保持响应。
        var result = await Task.Run(() => RawVerifier.CheckId(canonicalId, startDate, days))
            .ConfigureAwait(true);

        RawResultSummary = $"识别码：{result.Id}";
        RawMaxGapText = result.MaxGap.ToString("N0");
        RawHundredCountText = result.HundredCount.ToString("N0");
        foreach (var d in result.HundredDates)
        {
            _rawHundredDates.Add(d);
        }
        RawStatusText = $"原始计算完成。窗口 {days:N0} 天内出现 100 分 {result.HundredCount:N0} 次，最大间隔 {result.MaxGap:N0} 天。";
        RawBusy = false;
    }

    private void FillRawFromBest()
    {
        var best = SelectedResult;
        if (best is null)
        {
            return;
        }

        // 自动填入当前选中的最佳识别码，并沿用主界面的窗口参数，
        // 便于用户直接对比主扫描器与原始计算的结果。
        RawIdInput = best.Id;
        RawStartDate = StartDate;
        if (int.TryParse(DaysText, out var d) && d > 0)
        {
            RawDaysText = DaysText;
        }
        RawStatusText = $"已从主结果填入：{best.Id}。点击“原始计算”开始验算。";
    }

    /// <summary>
    /// 原始计算用的轻量规范化：去除短横线、转为大写、要求 16 位十六进制。
    /// 与 <see cref="IdFormat.TryNormalize"/> 不同，这里**不**强制首字符非 '0'，
    /// 因为原始计算是验算工具，边界用例（如 "0000-0000-000C-159C"）也必须能跑。
    /// </summary>
    private static string CanonicalizeForRawVerify(string raw)
    {
        var hex = raw.Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (hex.Length != IdFormat.HexLength)
        {
            throw new ArgumentException($"识别码长度必须为 16 位十六进制字符（去除短横线后），当前长度 {hex.Length}。");
        }

        foreach (var c in hex)
        {
            var ok = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
            if (!ok)
            {
                throw new ArgumentException("识别码只能包含 0-9 / A-F 十六进制字符。");
            }
        }

        return IdFormat.Format(hex);
    }

    private async Task StartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!TryParseInputs(out var count, out var days, out var k))
        {
            return;
        }

        var mode = SelectedMode;
        var info = new DateRangeInfo(StartDate, days);
        IEnumerable<string> ids;
        long? totalCount;
        Func<string, string?>? idNormalizer = null;

        if (CurrentSourceMode == SourceMode.Random)
        {
            // 固定格式生成器：16 位大写十六进制 4-4-4-4，首字符必非 '0'，恒有效。
            var generator = new RandomIdGenerator();
            ids = generator.TakeLong(count);
            totalCount = count;
        }
        else
        {
            if (!File.Exists(FilePath))
            {
                StatusText = "请选择有效的识别码文件。";
                return;
            }

            ids = FileIdSource.ReadLines(FilePath);
            totalCount = null;

            // 文件导入：逐行规范化验证（去空格/去横线/大写/16 位十六进制/首字符非 0），
            // 无效行返回 null 由处理器跳过并计数，不参与排名。
            idNormalizer = line => IdFormat.TryNormalize(line, out var normalized) ? normalized : null;
        }

        _cts = new CancellationTokenSource();

        ResetResult();
        IsIndeterminate = totalCount is null;
        if (totalCount is long total)
        {
            ProgressMaximum = total;
            ProgressValue = 0;
        }
        else
        {
            ProgressMaximum = 100;
            ProgressValue = 0;
        }

        IsBusy = true;
        StatusText = mode == ScanMode.MaxGap
            ? "正在计算最大间隔 Top-K…"
            : "正在计算距今最久 Top-K…";
        ProcessedText = "0";
        CurrentBestText = "暂无";
        ElapsedText = string.Empty;

        try
        {
            var progress = new Progress<RpProgressInfo>(OnProgress);

            var result = await ParallelRpProcessor.ProcessAsync(
                ids,
                info,
                totalCount,
                mode,
                k,
                progress,
                _cts.Token,
                idNormalizer,
                DefaultBatchSize);

            ApplyResult(result);
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消，显示当前已找到的最佳结果。";
        }
        catch (Exception ex)
        {
            StatusText = $"错误：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool TryParseInputs(
        out long count,
        out int days,
        out int k)
    {
        count = 0;
        days = 0;
        k = 0;

        if (CurrentSourceMode == SourceMode.Random)
        {
            if (!CountParser.TryParse(CountText, out count))
            {
                StatusText = "识别码数量格式无效。支持整数或科学计数法，如 1e10。";
                return false;
            }

            if (count <= 0 || count > MaxCount)
            {
                StatusText = $"识别码数量必须介于 1 和 {MaxCount:N0} 之间。";
                return false;
            }
        }

        if (!int.TryParse(KText, out k) || k < MinK || k > MaxK)
        {
            StatusText = $"K 值必须介于 {MinK} 和 {MaxK} 之间。";
            return false;
        }

        if (!int.TryParse(DaysText, out days) || days <= 0 || days > MaxWindowDays)
        {
            StatusText = $"窗口天数必须介于 1 和 {MaxWindowDays:N0} 之间。";
            return false;
        }

        return true;
    }

    private void OnProgress(RpProgressInfo info)
    {
        if (info.TotalCount is long total)
        {
            ProgressMaximum = total;
            ProgressValue = info.ProcessedCount;
            IsIndeterminate = false;
        }

        ProcessedText = info.TotalCount is long t
            ? $"{info.ProcessedCount:N0} / {t:N0}"
            : $"{info.ProcessedCount:N0}";

        InvalidCountText = info.InvalidCount > 0
            ? $"共跳过无效识别码：{info.InvalidCount:N0} 个"
            : string.Empty;

        CurrentBestText = info.CurrentBestMetric >= 0 && !string.IsNullOrEmpty(info.CurrentBestId)
            ? SelectedMode == ScanMode.MaxGap
                ? $"{info.CurrentBestId}（最大间隔 {info.CurrentBestMetric} 天，100分 {info.CurrentBestHundredCount} 次）"
                : $"{info.CurrentBestId}（首次100分第 {info.CurrentBestMetric} 天）"
            : "暂无";
    }

    private void ApplyResult(RpProcessingResult result)
    {
        // 下拉列表按“发现时间”排序（进入 Top-K 的先后顺序）。
        TopResults.Clear();
        foreach (var item in result.TopResults.OrderBy(x => x.DiscoveredAt))
        {
            TopResults.Add(item);
        }

        SelectedResult = result.Best;

        ElapsedText = $"{result.Elapsed.TotalSeconds:F2} 秒";
        ProcessedText = !IsIndeterminate && ProgressMaximum > 0
            ? $"{result.ProcessedCount:N0} / {ProgressMaximum:N0}"
            : result.ProcessedCount.ToString("N0");
        InvalidCountText = result.InvalidCount > 0
            ? $"共跳过无效识别码：{result.InvalidCount:N0} 个"
            : string.Empty;
        HasResult = result.TopResults.Count > 0;

        StatusText = result.IsCancelled
            ? "已取消。当前显示的是取消前找到的最佳结果，计算未完成。"
            : result.ProcessedCount > 0 && result.ProcessedCount == result.InvalidCount
                ? "计算完成。文件中没有有效的识别码。"
                : "计算完成。";
    }

    private void UpdateSelectedResultDetail()
    {
        var selected = SelectedResult;

        if (selected is null)
        {
            ResultId = string.Empty;
            ResultMetricText = "0";
            ResultHundredCountText = "0";
            ResultFirstDateText = string.Empty;
            HundredDates.Clear();
            return;
        }

        ResultId = selected.Id;
        ResultMetricText = selected.KeyMetric.ToString("N0");
        ResultHundredCountText = selected.HundredCount.ToString("N0");
        ResultFirstDateText = selected.Mode == ScanMode.First100Date
            ? selected.First100Date?.ToString("yyyy-MM-dd dddd") ?? string.Empty
            : selected.HundredDates.Count > 0
                ? selected.HundredDates[0].ToString("yyyy-MM-dd dddd")
                : string.Empty;

        HundredDates.Clear();
        foreach (var date in selected.HundredDates)
        {
            HundredDates.Add(date.ToString("yyyy-MM-dd dddd"));
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
        StatusText = "正在取消…";
    }

    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择识别码文件",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
        }
    }

    private async Task CopyResultAsync()
    {
        var selected = SelectedResult;
        if (selected is null)
        {
            return;
        }

        var lines = new List<string>
        {
            $"识别码: {selected.Id}",
            selected.Mode == ScanMode.MaxGap
                ? $"最大间隔天数: {selected.KeyMetric}"
                : $"第一个100分日期索引: {selected.KeyMetric}",
            $"100分日期数量: {selected.HundredCount}",
            $"首个100分日期: {ResultFirstDateText}",
            "100分日期:",
        };
        lines.AddRange(HundredDates);

        var text = string.Join(Environment.NewLine, lines);
        Clipboard.SetText(text);
        StatusText = "结果已复制到剪贴板。";
    }

    private void ResetResult()
    {
        TopResults.Clear();
        SelectedResult = null;
        ResultId = string.Empty;
        ResultMetricText = "0";
        ResultHundredCountText = "0";
        ResultFirstDateText = string.Empty;
        ElapsedText = string.Empty;
        InvalidCountText = string.Empty;
        HasResult = false;
        HundredDates.Clear();
    }
}
