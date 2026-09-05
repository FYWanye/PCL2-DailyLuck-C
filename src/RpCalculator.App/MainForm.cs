using System.Diagnostics;
using System.Runtime.InteropServices;
using RpCalculator.Core;

namespace RpCalculator.App;

public enum SourceMode
{
    Random,
    File
}

public sealed class MainForm : AntdUI.BorderlessForm
{
    private const long MaxCount = 10_000_000_000L;
    private const int MaxWindowDays = 100_000;
    private const int DefaultBatchSize = 100_000;
    private const int MinK = 1;
    private const int MaxK = 1000;

    // 布局
    private TableLayoutPanel _root = null!;
    private AntdUI.PageHeader _pageHeader = null!;
    private TableLayoutPanel _centerLayout = null!;
    private Panel _mainHost = null!;
    private AntdUI.Panel _sidebar = null!;
    private AntdUI.Panel _rightPanel = null!;
    private TableLayoutPanel _bottomBar = null!;
    private TableLayoutPanel _paramRow = null!;
    private readonly System.Windows.Forms.Timer _perfTimer = new() { Interval = 1000 };
    private readonly List<Control> _sections = new();
    private readonly List<AntdUI.Label> _captionLabels = new();
    private AntdUI.Menu _navMenu = null!;
    private bool _suppressNavEvent;
    private int _activeSection = -1;

    // 轻量页面切换动画（仅移动新页面，不改变最终 Dock 布局，保证稳定）。
    private const int SectionAnimDurationMs = 180;
    private const int SectionSlideOffset = 20;
    private System.Windows.Forms.Timer? _sectionAnimTimer;
    private bool _sectionAnimating;
    private int _sectionAnimTargetIndex = -1;
    private int _sectionAnimStartY;
    private DateTime _sectionAnimStartTime;
    private bool _startupEntrancePlayed;

    // Windows 通知（NotifyIcon 气泡）；仅在发通知时短暂出现在托盘区域，关闭后自动隐藏。
    private NotifyIcon? _notifyIcon;
    private System.Windows.Forms.Timer? _notifyHideTimer;

    // 右侧快速状态
    private AntdUI.Label _rightBestLabel = null!;
    private AntdUI.Label _rightProcessedLabel = null!;
    private AntdUI.Label _rightSpeedLabel = null!;
    private AntdUI.Label _rightEtaLabel = null!;
    private AntdUI.Progress _perfCpuBar = null!;
    private AntdUI.Progress _perfMemoryBar = null!;
    private AntdUI.Panel _rightPerfCard = null!;

    // 计算参数
    private AntdUI.Segmented _sourceSegmented = null!;
    private Control _countField = null!;
    private AntdUI.Input _countInput = null!;
    private AntdUI.Input _daysInput = null!;
    private AntdUI.Input _kInput = null!;
    private AntdUI.Input _fileInput = null!;
    private AntdUI.DatePicker _startDatePicker = null!;
    private AntdUI.Segmented _modeSegmented = null!;
    private AntdUI.Panel _filePanel = null!;
    private AntdUI.Button _startButton = null!;
    private AntdUI.Button _cancelButton = null!;
    private AntdUI.Button _browseButton = null!;

    // 进度
    private AntdUI.Progress _progress = null!;
    private AntdUI.Label _processedLabel = null!;
    private AntdUI.Label _elapsedLabel = null!;

    // 结果
    private AntdUI.Select _resultSelect = null!;
    private AntdUI.Input _resultFilterInput = null!;
    private AntdUI.Select _resultSortField = null!;
    private AntdUI.Segmented _resultSortDirection = null!;
    private bool _updatingResultList;
    private AntdUI.Input _resultIdInput = null!;
    private AntdUI.Label _resultMetricLabel = null!;
    private AntdUI.Label _resultHundredCountLabel = null!;
    private AntdUI.Label _resultFirstDateLabel = null!;
    private AntdUI.Switch _resultRecentSwitch = null!;
    private AntdUI.Label _resultRecentLabel = null!;
    private FlowLayoutPanel _resultDatesPanel = null!;
    private AntdUI.Button _copyButton = null!;

    // 性能监控
    private AntdUI.Label _perfCpuLabel = null!;
    private AntdUI.Label _perfMemoryLabel = null!;
    private AntdUI.Label _perfElapsedLabel = null!;
    private AntdUI.Label _perfSpeedLabel = null!;
    private AntdUI.Label _perfEtaLabel = null!;

    // 原始计算
    private AntdUI.Input _rawIdInput = null!;
    private AntdUI.DatePicker _rawStartDatePicker = null!;
    private AntdUI.Input _rawDaysInput = null!;
    private AntdUI.Button _rawComputeButton = null!;
    private AntdUI.Button _fillRawButton = null!;
    private AntdUI.Label _rawStatusLabel = null!;
    private AntdUI.Label _rawSummaryLabel = null!;
    private AntdUI.Label _rawMaxGapLabel = null!;
    private AntdUI.Label _rawHundredCountLabel = null!;
    private AntdUI.Label _rawFirstHundredLabel = null!;
    private AntdUI.Switch _rawRecentSwitch = null!;
    private AntdUI.Label _rawRecentLabel = null!;
    private FlowLayoutPanel _rawDatesPanel = null!;

    // 性能基准测试
    private AntdUI.Button _benchmarkButton = null!;
    private AntdUI.Button _cancelBenchmarkButton = null!;
    private AntdUI.Button _showBenchmarkBestButton = null!;
    private AntdUI.Label _benchmarkStatusLabel = null!;
    private AntdUI.Label _benchmarkResultLabel = null!;
    private AntdUI.Label _benchmarkBestLabel = null!;

    // 状态栏
    private AntdUI.Label _statusLabel = null!;

    // 状态
    private bool _isBusy;
    private bool _isRawBusy;
    private bool _isBenchmarkRunning;
    private bool _isDark;
    private CancellationTokenSource? _cts;
    private List<TopKResult> _topResults = new();
    private TopKResult? _selectedResult;
    private TopKResult? _benchmarkBest;

    // 性能监控
    private readonly Stopwatch _perfStopwatch = new();
    private CancellationTokenSource? _perfCts;
    private CancellationTokenSource? _perfLinkedCts;
    private long _perfProcessed;
    private long _lastPerfProcessed;
    private DateTime _lastPerfSampleTime;
    private TimeSpan _lastPerfCpu;
    private ulong _lastSystemIdle;
    private ulong _lastSystemKernel;
    private ulong _lastSystemUser;
    private bool _hasSystemCpuSample;
    private long? _perfTotal;

    // 基准测试
    private CancellationTokenSource? _benchmarkCts;
    private Stopwatch? _benchmarkStopwatch;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private static double GetTotalPhysicalMemoryMb()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status) ? status.ullTotalPhys / 1024.0 / 1024.0 : 8192.0;
    }

    private static bool GetPhysicalMemoryMb(out double totalMb, out double availableMb)
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref status))
        {
            totalMb = status.ullTotalPhys / 1024.0 / 1024.0;
            availableMb = status.ullAvailPhys / 1024.0 / 1024.0;
            return true;
        }

        totalMb = 8192.0;
        availableMb = 8192.0;
        return false;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    private static ulong ToUInt64(FILETIME ft)
    {
        return ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
    }
    public MainForm()
    {
        InitializeComponent();
        ApplyTheme();
        InitializeNotificationIcon();
    }

    private void InitializeComponent()
    {
        Text = "今日人品间隔分析器";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 800);
        MinimumSize = new Size(900, 700);
        Resizable = true;
        // 只保留圆角窗口；不使用外层阴影/边框，避免出现背景框。
        Radius = DesignTokens.RoundedLg;
        UseDwm = false;
        Shadow = 0;
        ShadowColor = Color.Transparent;
        BorderWidth = 0;
        ShowInTaskbar = true;

        _root = new TableLayoutPanel
        {
            // 根容器使用不透明背景，避免透明层叠造成控件显示异常。
            BackColor = DesignTokens.WindowBackground(_isDark),
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0)
        };
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.HeaderHeight));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // 底部行高 = 底部状态区高度，避免按钮/进度被裁剪。
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));

        var header = CreateHeader();
        _root.Controls.Add(header, 0, 0);
        _root.Controls.Add(CreateCenter(), 0, 1);
        _root.Controls.Add(CreateBottomBar(), 0, 2);

        Controls.Add(_root);
        AddResizeGrips();

        UpdateSourceVisibility();
        ShowSection(0);
    }

    private Control CreateHeader()
    {
        _pageHeader = new AntdUI.PageHeader
        {
            Text = "今日人品间隔分析器",
            SubText = "计算 100 分日期最大间隔 / 距今最久 Top-K",
            Dock = DockStyle.Fill,
            Height = DesignTokens.HeaderHeight,
            ShowIcon = true,
            ShowButton = true,
            MaximizeBox = true,
            MinimizeBox = true,
            DividerShow = true,
            UseLeftMargin = true,
            Padding = new Padding(12, 0, 12, 0)
        };

        // 标题栏右侧操作：直接以 Dock=Right 挂在 PageHeader 上，避免额外容器出现背景框。
        var themeButton = new AntdUI.Button
        {
            Text = "🌗 主题",
            Dock = DockStyle.Right,
            AutoSize = false,
            Size = new Size(88, DesignTokens.ControlHeight),
            Radius = DesignTokens.RoundedMd,
            BorderWidth = 1,
            Type = AntdUI.TTypeMini.Default,
            Cursor = Cursors.Hand
        };
        themeButton.Click += (_, _) => ToggleTheme();
        _pageHeader.Controls.Add(themeButton);


        return _pageHeader;
    }
    private Control CreateCenter()
    {
        _centerLayout = new TableLayoutPanel
        {
            BackColor = DesignTokens.WindowBackground(_isDark),
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0)
        };
        _centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        _centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

        _centerLayout.Controls.Add(CreateSidebar(), 0, 0);
        _centerLayout.Controls.Add(CreateMainHost(), 1, 0);
        _centerLayout.Controls.Add(CreateRightPanel(), 2, 0);
        return _centerLayout;
    }

    private Control CreateSidebar()
    {
        _sidebar = new AntdUI.Panel
        {
            Dock = DockStyle.Fill,
            Radius = 0,
            BorderWidth = 0,
            InnerPadding = new Padding(0),
            AutoSize = false
        };

        _navMenu = new AntdUI.Menu
        {
            Dock = DockStyle.Fill,
            Mode = AntdUI.TMenuMode.Vertical,
            Indent = true,
            Gap = 4,
            Radius = DesignTokens.RoundedMd,
            Padding = new Padding(8, 12, 8, 8),
            AutoSize = false
        };
        _navMenu.Items.Add(new AntdUI.MenuItem { Text = "欢迎", Tag = 0 });
        _navMenu.Items.Add(new AntdUI.MenuItem { Text = "计算参数", Tag = 1 });
        _navMenu.Items.Add(new AntdUI.MenuItem { Text = "分析结果", Tag = 2 });
        _navMenu.Items.Add(new AntdUI.MenuItem { Text = "原始计算", Tag = 3 });
        _navMenu.Items.Add(new AntdUI.MenuItem { Text = "性能基准测试", Tag = 4 });
        _navMenu.Items.Add(new AntdUI.MenuItem { Text = "关于", Tag = 5 });
        _navMenu.SelectChanged += (_, e) =>
        {
            if (_suppressNavEvent) return;
            if (e.Value is AntdUI.MenuItem item && item.Tag is int index) ShowSection(index);
        };

        _sidebar.Controls.Add(_navMenu);
        return _sidebar;
    }

    private void SyncNavSelection()
    {
        if (_navMenu is null || _navMenu.Items.Count == 0 || _activeSection < 0 || _activeSection >= _navMenu.Items.Count)
        {
            return;
        }

        var item = _navMenu.Items[_activeSection];
        if (item.Select) return;

        _suppressNavEvent = true;
        _navMenu.Select(item, false);
        _suppressNavEvent = false;
    }

    private Control CreateMainHost()
    {
        _mainHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(DesignTokens.SpacingLg),
            BackColor = Color.Transparent
        };

        // 每个功能独立成节，只显示当前选中的节。
        AddSection(CreateWelcomeCard(), 520);
        AddSection(CreateInputCard(), 302);
        AddSection(CreateResultCard(), 560);
        AddSection(CreateRawCard(), 548);
        AddSection(CreateBenchmarkCard(), 308);
        AddSection(CreateAboutCard(), 520);
        return _mainHost;
    }

    private void AddSection(Control card, int height)
    {
        card.Dock = DockStyle.Top;
        card.AutoSize = false;
        card.Height = height;
        card.Margin = new Padding(0, 0, 0, DesignTokens.CardGap);
        card.Visible = false;
        _mainHost.Controls.Add(card);
        _sections.Add(card);
    }

    private void ShowSection(int index)
    {
        if (index < 0 || index >= _sections.Count)
        {
            return;
        }

        // 若上一段动画还没结束，先把当前页恢复到标准 Dock 布局，避免残留“绝对定位”状态。
        CompleteSectionAnimation();

        if (index == _activeSection)
        {
            return;
        }

        var fromIndex = _activeSection;
        _activeSection = index;

        // 首次构造/窗口尚未显示时不播动画，直接完成切换（启动淡入负责欢迎页的入场）。
        if (!IsHandleCreated || !Visible)
        {
            ShowSectionCore(index);
            return;
        }

        StartSectionAnimation(fromIndex, index);
    }

    /// <summary>无动画地切换页面，并把所有页面恢复成标准 Dock=Top 布局。</summary>
    private void ShowSectionCore(int index)
    {
        if (_mainHost is null || _sections.Count == 0)
        {
            return;
        }

        _mainHost.SuspendLayout();
        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            if (section.Dock != DockStyle.Top)
            {
                section.Dock = DockStyle.Top;
                section.Margin = new Padding(0, 0, 0, DesignTokens.CardGap);
            }

            section.Visible = i == index;
        }

        _mainHost.AutoScrollMinSize = new Size(0, _sections[index].Height + DesignTokens.SpacingLg);
        _mainHost.AutoScrollPosition = Point.Empty;
        _mainHost.ResumeLayout();
        _mainHost.PerformLayout();

        UpdateActionButtonsTheme();
        SyncNavSelection();
    }

    /// <summary>启动新页面从下方轻微滑入的动画；期间保持非 Dock 定位，结束后立即还原。</summary>
    private void StartSectionAnimation(int fromIndex, int index)
    {
        var section = _sections[index];
        var startY = _mainHost.Padding.Top + SectionSlideOffset;

        _mainHost.SuspendLayout();
        if (fromIndex >= 0 && fromIndex < _sections.Count)
        {
            _sections[fromIndex].Visible = false;
        }

        section.Visible = true;
        section.Dock = DockStyle.None;
        // 左右都锚定：父容器宽度变化时由布局自动调宽，动画帧里不再反复触发子控件布局。
        section.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        section.Margin = Padding.Empty;
        section.Location = new Point(_mainHost.Padding.Left, startY);
        if (_mainHost.ClientSize.Width > 0)
        {
            section.Width = Math.Max(0, _mainHost.ClientSize.Width - _mainHost.Padding.Horizontal);
        }

        // 动画期间从顶部开始；结束后 ShowSectionCore 会按当前页重新设置可滚动高度。
        _mainHost.AutoScrollPosition = Point.Empty;
        _mainHost.ResumeLayout();
        _mainHost.PerformLayout();

        _sectionAnimating = true;
        _sectionAnimTargetIndex = index;
        _sectionAnimStartY = startY;
        _sectionAnimStartTime = DateTime.UtcNow;

        if (_sectionAnimTimer is null)
        {
            _sectionAnimTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _sectionAnimTimer.Tick += OnSectionAnimationTick;
        }

        _sectionAnimTimer.Start();

        UpdateActionButtonsTheme();
        SyncNavSelection();
    }

    private void OnSectionAnimationTick(object? sender, EventArgs e)
    {
        if (!_sectionAnimating || _sectionAnimTargetIndex < 0 || _sectionAnimTargetIndex >= _sections.Count)
        {
            CompleteSectionAnimation();
            return;
        }

        var elapsed = (DateTime.UtcNow - _sectionAnimStartTime).TotalMilliseconds;
        var progress = Math.Min(1.0, elapsed / SectionAnimDurationMs);
        // EaseOutCubic：先快后慢，视觉上更顺滑，且不会产生过冲。
        var eased = 1.0 - Math.Pow(1.0 - progress, 3);

        var section = _sections[_sectionAnimTargetIndex];
        if (section.IsDisposed)
        {
            CompleteSectionAnimation();
            return;
        }

        var targetY = _mainHost.Padding.Top;
        var y = (int)Math.Round(_sectionAnimStartY + (targetY - _sectionAnimStartY) * eased);
        // 只移动 Y，宽度交给 Anchor 自动处理，避免每帧触发子控件重新布局。
        section.Location = new Point(_mainHost.Padding.Left, y);

        if (progress >= 1.0)
        {
            CompleteSectionAnimation();
        }
    }

    /// <summary>结束动画：停止定时器，并把正在展示的页面还原为 Dock=Top 的标准布局。</summary>
    private void CompleteSectionAnimation()
    {
        if (_sectionAnimTimer is not null)
        {
            _sectionAnimTimer.Stop();
        }

        var animIndex = _sectionAnimTargetIndex;
        _sectionAnimTargetIndex = -1;
        _sectionAnimating = false;

        if (animIndex >= 0 && animIndex < _sections.Count && _activeSection >= 0)
        {
            var section = _sections[animIndex];
            if (!section.IsDisposed && _activeSection == animIndex)
            {
                ShowSectionCore(animIndex);
                return;
            }
        }

        // 没有需要还原的页面时，至少把可见状态与菜单同步一次。
        SyncNavSelection();
    }
    private Control CreateRightPanel()
    {
        _rightPanel = new AntdUI.Panel
        {
            Dock = DockStyle.Fill,
            Back = Color.Transparent,
            BackColor = Color.Transparent,
            Radius = 0,
            BorderWidth = 0,
            InnerPadding = new Padding(16),
            AutoSize = false
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        flow.Controls.Add(new AntdUI.Label
        {
            Text = "快速状态",
            AutoSize = true,
            Font = new Font(AppleTheme.FontFamily, 11F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        });
        flow.Controls.Add(CreateCaptionLabel("当前全局最佳"));
        _rightBestLabel = CreateValueLabel("暂无");
        _rightBestLabel.TextMultiLine = true;
        _rightBestLabel.Width = 256;
        flow.Controls.Add(_rightBestLabel);
        flow.Controls.Add(CreateCaptionLabel("已处理"));
        _rightProcessedLabel = CreateValueLabel("0");
        flow.Controls.Add(_rightProcessedLabel);
        flow.Controls.Add(CreateCaptionLabel("当前速度"));
        _rightSpeedLabel = CreateValueLabel("—");
        flow.Controls.Add(_rightSpeedLabel);
        flow.Controls.Add(CreateCaptionLabel("预计剩余"));
        _rightEtaLabel = CreateValueLabel("—");
        flow.Controls.Add(_rightEtaLabel);

        flow.Controls.Add(CreateRightPerfCard());

        _rightPanel.Controls.Add(flow);
        return _rightPanel;
    }

    private Control CreateRightPerfCard()
    {
        var card = new AntdUI.Panel
        {
            Back = Color.Transparent,
            BackColor = Color.Transparent,
            Radius = 0,
            BorderWidth = 0,
            InnerPadding = new Padding(0),
            Width = 208,
            Height = 280,
            AutoSize = false,
            Margin = new Padding(0, 12, 0, 0)
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        flow.Controls.Add(new AntdUI.Label
        {
            Text = "性能监控",
            AutoSize = true,
            Font = new Font(AppleTheme.FontFamily, 10.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6)
        });

        flow.Controls.Add(CreateCaptionLabel("CPU 占用"));
        _perfCpuLabel = CreateValueLabel("0%");
        flow.Controls.Add(_perfCpuLabel);
        _perfCpuBar = new AntdUI.Progress
        {
            Width = 184,
            Height = 20,
            Radius = 4,
            Value = 0F,
            ShowTextDot = 0,
            Text = "",
            Margin = new Padding(0, 0, 0, 4)
        };
        flow.Controls.Add(_perfCpuBar);

        flow.Controls.Add(CreateCaptionLabel("内存占用"));
        _perfMemoryLabel = CreateValueLabel("0 MB");
        flow.Controls.Add(_perfMemoryLabel);
        _perfMemoryBar = new AntdUI.Progress
        {
            Width = 184,
            Height = 20,
            Radius = 4,
            Value = 0F,
            ShowTextDot = 0,
            Text = "",
            Margin = new Padding(0, 0, 0, 4)
        };
        flow.Controls.Add(_perfMemoryBar);

        flow.Controls.Add(CreateCaptionLabel("已运行"));
        _perfElapsedLabel = CreateValueLabel("00:00:00");
        flow.Controls.Add(_perfElapsedLabel);

        flow.Controls.Add(CreateCaptionLabel("实时速率"));
        _perfSpeedLabel = CreateValueLabel("0 条/秒");
        flow.Controls.Add(_perfSpeedLabel);

        flow.Controls.Add(CreateCaptionLabel("预计剩余"));
        _perfEtaLabel = CreateValueLabel("—");
        flow.Controls.Add(_perfEtaLabel);

        _rightPerfCard = card;
        card.Controls.Add(flow);
        return card;
    }

    private Control CreateBottomBar()
    {
        // 底部状态区：不再使用白色“衬底”；同时把处理进度/耗时直接放在这里。
        _bottomBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(24, 20, 24, 20)
        };
        _bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0)
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _statusLabel = new AntdUI.Label
        {
            Dock = DockStyle.Fill,
            Text = "就绪。",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DesignTokens.TextSecondary
        };
        left.Controls.Add(_statusLabel, 0, 0);

        _progress = new AntdUI.Progress
        {
            Dock = DockStyle.Fill,
            Height = 22,
            Radius = 11,
            Value = 0F,
            ShowTextDot = 0,
            Text = "",
            Margin = new Padding(0, 2, 20, 2)
        };
        left.Controls.Add(_progress, 0, 1);

        var infoRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _processedLabel = new AntdUI.Label
        {
            Dock = DockStyle.Fill,
            Text = "0",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(AppleTheme.FontFamily, 9F)
        };
        _elapsedLabel = new AntdUI.Label
        {
            Dock = DockStyle.Fill,
            Text = "00:00:00",
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font(AppleTheme.FontFamily, 9F)
        };
        infoRow.Controls.Add(_processedLabel, 0, 0);
        infoRow.Controls.Add(_elapsedLabel, 1, 0);
        _processedLabel.Visible = false;
        _elapsedLabel.Visible = false;
        left.Controls.Add(infoRow, 0, 2);
        _bottomBar.Controls.Add(left, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 22, 0, 22)
        };
        _startButton = CreateButton("开始计算", () => StartAsync(), primary: true);
        _startButton.AutoSize = false;
        _startButton.Size = new Size(150, 44);
        _startButton.Font = new Font(AppleTheme.FontFamily, 10.5F, FontStyle.Bold);
        _cancelButton = CreateButton("取消", Cancel, danger: true);
        _cancelButton.AutoSize = false;
        _cancelButton.Size = new Size(88, 44);
        _cancelButton.Enabled = false;
        _cancelButton.Margin = new Padding(8, 0, 0, 0);
        // 没有白色衬底后，给取消按钮明确的表面色与红色文字，避免和背景糊在一起。
        _cancelButton.Type = AntdUI.TTypeMini.Default;
        _cancelButton.BackColor = DesignTokens.SurfaceColor(_isDark);
        _cancelButton.BackHover = DesignTokens.SurfaceHoverColor(_isDark);
        _cancelButton.BackActive = DesignTokens.SurfaceHoverColor(_isDark);
        _cancelButton.ForeColor = DesignTokens.DangerStrong;
        _cancelButton.ForeHover = DesignTokens.DangerStrong;
        _cancelButton.ForeActive = DesignTokens.DangerStrong;
        buttons.Controls.Add(_startButton);
        buttons.Controls.Add(_cancelButton);
        _bottomBar.Controls.Add(buttons, 1, 0);

        return _bottomBar;
    }
    private void AddResizeGrips()
    {
        // 只加左侧与底部热区：右上角窗口按钮必须保持完整可点击，
        // 因此不在右/上侧叠加自定义 grip，交给 AntdUI 原生 8px 热区处理。
        var left = new Panel
        {
            Dock = DockStyle.Left,
            Width = 8,
            Cursor = Cursors.SizeWE,
            BackColor = Color.Transparent
        };
        left.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                WindowChrome.StartResize(this, WindowChrome.HTLEFT);
            }
        };

        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 8,
            Cursor = Cursors.SizeNS,
            BackColor = Color.Transparent
        };
        bottom.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                WindowChrome.StartResize(this, WindowChrome.HTBOTTOM);
            }
        };

        Controls.Add(left);
        Controls.Add(bottom);
        left.BringToFront();
        bottom.BringToFront();
    }

    private AntdUI.Panel CreateCardShell()
    {
        // 不再使用白色“衬底”卡片：页面内容直接放在窗口背景上。
        return new AntdUI.Panel
        {
            Back = Color.Transparent,
            BackColor = Color.Transparent,
            Radius = 0,
            BorderWidth = 0,
            InnerPadding = new Padding(0),
            AutoSize = false
        };
    }
    private Control CreateWelcomeCard()
    {
        var card = CreateCardShell();
        var body = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(0)
        };

        // 高清图标区 / 标题 / 副标题 / 简介 / 快捷操作 / 提示 / 弹性留白
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 高清图标：使用 Assets/app-256.png（256×256），比从 ICO 默认读取的 32×32 更清晰。
        var iconBox = new PictureBox
        {
            Size = new Size(132, 132),
            Anchor = AnchorStyles.None,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        var appIcon = LoadAppIconImage();
        if (appIcon is not null)
        {
            iconBox.Image = appIcon;
        }
        body.Controls.Add(iconBox, 0, 0);

        var title = new AntdUI.Label
        {
            Text = "今日人品间隔分析器",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(AppleTheme.FontFamily, 24F, FontStyle.Bold)
        };
        body.Controls.Add(title, 0, 1);

        var subtitle = new AntdUI.Label
        {
            Text = "快速定位 100 分日期最大间隔 / 距今最久的 Top-K 识别码",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = DesignTokens.TextSecondaryColor(_isDark),
            Font = new Font(AppleTheme.FontFamily, 12F)
        };
        _captionLabels.Add(subtitle);
        body.Controls.Add(subtitle, 0, 2);

        var desc = new AntdUI.Label
        {
            Text = "支持随机生成或从文件导入识别码，并提供原始算法验算、性能基准测试与实时性能监控。",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.TopCenter,
            TextMultiLine = true,
            ForeColor = DesignTokens.TextSecondaryColor(_isDark),
            Font = new Font(AppleTheme.FontFamily, 10.5F),
            Padding = new Padding(60, 14, 60, 0)
        };
        _captionLabels.Add(desc);
        body.Controls.Add(desc, 0, 3);

        // 快捷入口：让欢迎页不只是静态展示，也能一键进入常用功能。
        var actionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(40, 0, 40, 0)
        };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        actionRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var startButton = CreateButton("开始计算", () => ShowSection(1), primary: true);
        startButton.AutoSize = false;
        startButton.Size = new Size(132, 40);
        startButton.Anchor = AnchorStyles.None;

        var benchmarkButton = CreateButton("性能测试", () => ShowSection(4));
        benchmarkButton.AutoSize = false;
        benchmarkButton.Size = new Size(132, 40);
        benchmarkButton.Anchor = AnchorStyles.None;

        var aboutButton = CreateButton("关于项目", () => ShowSection(5));
        aboutButton.AutoSize = false;
        aboutButton.Size = new Size(132, 40);
        aboutButton.Anchor = AnchorStyles.None;

        actionRow.Controls.Add(startButton, 0, 0);
        actionRow.Controls.Add(benchmarkButton, 1, 0);
        actionRow.Controls.Add(aboutButton, 2, 0);
        body.Controls.Add(actionRow, 0, 4);

        var hint = new AntdUI.Label
        {
            Text = "也可以从左侧导航进入功能页，或直接点击底部“开始计算”。",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = DesignTokens.TextSecondaryColor(_isDark),
            Font = new Font(AppleTheme.FontFamily, 9.5F)
        };
        _captionLabels.Add(hint);
        body.Controls.Add(hint, 0, 5);

        card.Controls.Add(body);
        return card;
    }

    private Control CreateAboutCard()
    {
        var card = CreateCardShell();
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(4),
            BackColor = Color.Transparent
        };

        var title = new AntdUI.Label
        {
            Text = "关于与致谢",
            AutoSize = true,
            Font = new Font(AppleTheme.FontFamily, 16F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };
        flow.Controls.Add(title);

        flow.Controls.Add(CreateCaptionLabel("项目灵感"));
        flow.Controls.Add(CreateLinkLabel("0x1A5 的视频", "https://www.bilibili.com/video/BV1PLgK6XEyy/"));

        flow.Controls.Add(CreateCaptionLabel("算法来源"));
        flow.Controls.Add(CreateLinkLabel("Zyx-2012 / daily-luck", "https://github.com/Zyx-2012/daily-luck"));

        flow.Controls.Add(CreateCaptionLabel("项目重构前的 Rust 版（也是我做的）"));
        flow.Controls.Add(CreateLinkLabel("FYWanye / daily-luck-rust", "https://github.com/FYWanye/daily-luck-rust"));

        flow.Controls.Add(CreateCaptionLabel("当前 C# 重构项目"));
        flow.Controls.Add(CreateLinkLabel("FYWanye / PCL2-DailyLuck-C", "https://github.com/FYWanye/PCL2-DailyLuck-C"));

        flow.Controls.Add(CreateCaptionLabel("我的 GitHub"));
        flow.Controls.Add(CreateLinkLabel("https://github.com/FYWanye/", "https://github.com/FYWanye/"));

        card.Controls.Add(flow);
        return card;
    }

    private static Image? LoadAppIconImage()
    {
        try
        {
            // 欢迎页优先使用高清 256×256 PNG，比从 ico 读取的 32×32 更清晰。
            var pngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-256.png");
            if (File.Exists(pngPath))
            {
                return Image.FromFile(pngPath);
            }

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                using var stream = File.OpenRead(iconPath);
                using var icon = new Icon(stream);
                return icon.ToBitmap();
            }

            using var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            return exeIcon?.ToBitmap();
        }
        catch
        {
            return null;
        }
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }

            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            return null;
        }
    }

    private void InitializeNotificationIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon() ?? SystemIcons.Application,
            Text = "今日人品间隔分析器",
            Visible = false
        };

        // 通知关闭后立即隐藏托盘图标；再加一个兜底定时器，防止系统未触发 Closed 时残留图标。
        _notifyHideTimer = new System.Windows.Forms.Timer { Interval = 8000 };
        _notifyHideTimer.Tick += (_, _) => HideNotificationIcon();

        _notifyIcon.BalloonTipClosed += (_, _) => HideNotificationIcon();
    }

    private void HideNotificationIcon()
    {
        _notifyHideTimer?.Stop();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }
    }

    /// <summary>在应用窗口内部顶部显示 AntdUI 消息，避免弹到屏幕顶部。</summary>
    private void ShowInAppMessage(string text)
    {
        try
        {
            var config = new AntdUI.Message.Config(this, text, AntdUI.TType.Success)
            {
                ShowInWindow = true,
                Align = AntdUI.TAlignFrom.Top,
                TopMost = false,
                AutoClose = 4,
                ClickClose = true
            };
            AntdUI.Message.open(config);
        }
        catch
        {
            // 应用内消息只是增强反馈，失败时静默，不影响主流程。
        }
    }

    /// <summary>发送 Windows 原生气泡通知；托盘图标只在该通知期间出现。</summary>
    private void ShowWindowsNotification(string title, string text)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        try
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.Text = title.Length <= 63 ? title : title[..63];
            _notifyIcon.Visible = true;
            _notifyIcon.ShowBalloonTip(3000);

            _notifyHideTimer?.Stop();
            _notifyHideTimer?.Start();
        }
        catch
        {
            // Windows 通知失败时保留应用内提示即可。
        }
    }

    private static AntdUI.Button CreateLinkLabel(string text, string url)
    {
        var button = new AntdUI.Button
        {
            Text = $"↗ {text}",
            AutoSize = true,
            Radius = DesignTokens.RoundedSm,
            BorderWidth = 1,
            Type = AntdUI.TTypeMini.Default,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 10)
        };
        button.Click += (_, _) => OpenLink(url);
        return button;
    }
    private static void OpenLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开链接：{ex.Message}", "打开链接失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private Control CreateInputCard()
    {
        var card = CreateCardShell();
        var body = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 1,
            Padding = new Padding(0)
        };

        // 来源：随机 / 文件
        _sourceSegmented = new AntdUI.Segmented
        {
            AutoSize = false,
            Height = DesignTokens.ControlHeight,
            Radius = DesignTokens.RoundedMd,
            Gap = 4
        };
        _sourceSegmented.Items.Add(new AntdUI.SegmentedItem { Text = "随机生成" });
        _sourceSegmented.Items.Add(new AntdUI.SegmentedItem { Text = "从文件导入" });
        _sourceSegmented.SelectIndex = 0;
        _sourceSegmented.SelectIndexChanged += (_, _) => UpdateSourceVisibility();
        AddBodyRow(body, _sourceSegmented, DesignTokens.ControlHeight);

        // 参数行：数量 / 窗口天数 / K
        _countField = CreateField("识别码数量（支持 1e10）", out _countInput, "1000000");
        _paramRow = CreateTwoOrThreeColumnRow(
            _countField,
            CreateField("窗口天数", out _daysInput, "1780"),
            CreateField("K 值（Top-K，1~1000）", out _kInput, "10"));
        AddBodyRow(body, _paramRow, 56);

        // 文件路径（默认隐藏）
        _filePanel = new AntdUI.Panel
        {
            AutoSize = false,
            Height = DesignTokens.ControlHeight,
            Radius = DesignTokens.RoundedMd,
            BorderWidth = 0,
            InnerPadding = new Padding(0)
        };
        var fileRow = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 2,
            Padding = new Padding(0)
        };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fileRow.RowCount = 1;
        fileRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _fileInput = CreateInput("选择识别码文件（每行一个）");
        _browseButton = CreateButton("浏览…", () => BrowseFile());
        _browseButton.AutoSize = false;
        _browseButton.Size = new Size(88, DesignTokens.ControlHeight);
        _browseButton.Margin = new Padding(8, 0, 0, 0);
        fileRow.Controls.Add(_fileInput, 0, 0);
        fileRow.Controls.Add(_browseButton, 1, 0);
        _filePanel.Controls.Add(fileRow);
        AddBodyRow(body, _filePanel, DesignTokens.ControlHeight);

        // 日期 + 模式
        var dateModeRow = CreateTwoColumnRow(
            CreateField("起始日期", out _startDatePicker, null),
            CreateField("算法模式", out _modeSegmented, null));
        AddBodyRow(body, dateModeRow, 56);
        _modeSegmented.SelectIndexChanged += (_, _) => UpdateModeSubtitle();

        AddBodyRow(body, CreateCaptionLabel("点击底部“开始计算”运行任务。"), 30);

        card.Controls.Add(body);
        return card;
    }

    private Control CreateResultCard()
    {
        var card = CreateCardShell();
        var body = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 1,
            Padding = new Padding(0)
        };

        // 结果选择区：筛选 + 排序依据 + 升/降序 + 候选下拉 + 复制
        var filterRow = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 40,
            AutoSize = false,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _resultFilterInput = CreateInput("筛选识别码，如 7123");
        _resultFilterInput.Margin = new Padding(0, 0, 8, 0);
        _resultFilterInput.TextChanged += (_, _) => RefreshResultItems();

        _copyButton = CreateButton("复制结果", CopyResult);
        _copyButton.AutoSize = false;
        _copyButton.Size = new Size(96, DesignTokens.ControlHeight);
        _copyButton.Enabled = false;

        filterRow.Controls.Add(_resultFilterInput, 0, 0);
        filterRow.Controls.Add(_copyButton, 1, 0);
        AddBodyRow(body, filterRow, DesignTokens.ControlHeight);

        var sortRow = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 40,
            AutoSize = false,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        sortRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sortRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        sortRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _resultSortField = new AntdUI.Select
        {
            Dock = DockStyle.Fill,
            Height = 40,
            Radius = DesignTokens.RoundedMd,
            DropDownRadius = DesignTokens.RoundedMd,
            Margin = new Padding(0, 0, 8, 0)
        };
        _resultSortField.Items.Add(new AntdUI.SelectItem("发现顺序", 0));
        _resultSortField.Items.Add(new AntdUI.SelectItem("关键指标", 1));
        _resultSortField.Items.Add(new AntdUI.SelectItem("100分次数", 2));
        _resultSortField.Items.Add(new AntdUI.SelectItem("识别码", 3));
        _resultSortField.SelectedIndex = 1;
        _resultSortField.SelectedIndexChanged += (_, _) => RefreshResultItems();

        _resultSortDirection = new AntdUI.Segmented
        {
            Dock = DockStyle.Fill,
            Height = 40,
            Radius = DesignTokens.RoundedMd,
            Gap = 4
        };
        _resultSortDirection.Items.Add(new AntdUI.SegmentedItem { Text = "↑ 升序" });
        _resultSortDirection.Items.Add(new AntdUI.SegmentedItem { Text = "↓ 倒序" });
        _resultSortDirection.SelectIndex = 1;
        _resultSortDirection.SelectIndexChanged += (_, _) => RefreshResultItems();

        sortRow.Controls.Add(_resultSortField, 0, 0);
        sortRow.Controls.Add(_resultSortDirection, 1, 0);
        AddBodyRow(body, sortRow, DesignTokens.ControlHeight);

        var selectRow = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 40,
            AutoSize = false,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(0)
        };
        selectRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _resultSelect = new AntdUI.Select
        {
            Dock = DockStyle.Fill,
            Height = 40,
            Radius = DesignTokens.RoundedMd,
            PlaceholderText = "候选识别码（Top-K）",
            DropDownRadius = DesignTokens.RoundedMd
        };
        _resultSelect.SelectedIndexChanged += (_, _) =>
        {
            if (!_updatingResultList) UpdateSelectedResultDetail();
        };
        selectRow.Controls.Add(_resultSelect, 0, 0);
        AddBodyRow(body, selectRow, DesignTokens.ControlHeight);
        // 详情行：识别码框占满左侧，指标放右侧，统一 56px 高。
        var detailRow = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 56,
            AutoSize = false,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        detailRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        detailRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        detailRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var idField = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 56,
            AutoSize = false,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0, 0, 12, 0)
        };
        idField.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        idField.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.ControlHeight));
        idField.Controls.Add(CreateCaptionLabel("识别码（可选中复制）"), 0, 0);
        _resultIdInput = CreateInput("", readOnly: true);
        _resultIdInput.Height = DesignTokens.ControlHeight;
        _resultIdInput.Font = new Font(DesignTokens.MonoFontFamily, 10.5F, FontStyle.Bold);
        idField.Controls.Add(_resultIdInput, 0, 1);
        detailRow.Controls.Add(idField, 0, 0);

        var metricField = CreateTwoColumnRow(
            CreateMetricField("关键指标", out _resultMetricLabel),
            CreateMetricField("100分次数", out _resultHundredCountLabel));
        detailRow.Controls.Add(metricField, 1, 0);

        AddBodyRow(body, detailRow, 56);

        var recentRow = new FlowLayoutPanel
        {
            AutoSize = false,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        var recentCaption = CreateCaptionLabel("自动查找最近一次满分日期（从今天往前）");
        _resultRecentSwitch = new AntdUI.Switch
        {
            AutoSize = false,
            Size = new Size(44, 22),
            Checked = true,
            Margin = new Padding(8, 0, 12, 0)
        };
        _resultRecentSwitch.CheckedChanged += async (_, _) => await SearchResultRecentHundredAsync();
        _resultRecentLabel = CreateValueLabel("未启用");
        recentRow.Controls.Add(recentCaption);
        recentRow.Controls.Add(_resultRecentSwitch);
        recentRow.Controls.Add(_resultRecentLabel);
        AddBodyRow(body, recentRow, 34);

        AddBodyRow(body, CreateCaptionLabel("首个 100 分日期"), 24);
        _resultFirstDateLabel = CreateValueLabel("");
        AddBodyRow(body, _resultFirstDateLabel, 28);

        AddBodyRow(body, CreateCaptionLabel("100 分日期（可滚动）"), 24);
        _resultDatesPanel = CreateDateListPanel();
        AddBodyRow(body, _resultDatesPanel, 170);

        card.Controls.Add(body);
        return card;
    }

    private Control CreateRawCard()
    {
        var card = CreateCardShell();
        var body = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 1,
            Padding = new Padding(0)
        };

        var topRow = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight,
            AutoSize = false,
            ColumnCount = 2,
            Padding = new Padding(0)
        };
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var desc = CreateCaptionLabel("使用独立、未优化的逐日算法重新计算，结果用于核对主扫描器。");
        _fillRawButton = CreateButton("填入最佳", FillRawFromBest);
        _fillRawButton.AutoSize = false;
        _fillRawButton.Size = new Size(96, DesignTokens.ControlHeight);
        _fillRawButton.Enabled = false;
        topRow.Controls.Add(desc, 0, 0);
        topRow.Controls.Add(_fillRawButton, 1, 0);
        AddBodyRow(body, topRow, DesignTokens.ControlHeight);

        AddBodyRow(body, CreateField("识别码（可手动输入，4-4-4-4 格式自动格式化）", out _rawIdInput, ""), 56);
        var dateDaysRow = CreateTwoColumnRow(
            CreateField("起始日期", out _rawStartDatePicker, null),
            CreateField("窗口天数", out _rawDaysInput, "1780"));
        AddBodyRow(body, dateDaysRow, 56);

        var buttonRow = new FlowLayoutPanel
        {
            AutoSize = false,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        _rawComputeButton = CreateButton("原始计算", RawComputeAsync, primary: true);
        _rawStatusLabel = CreateValueLabel("输入识别码并点击“原始计算”以独立验算。");
        buttonRow.Controls.Add(_rawComputeButton);
        buttonRow.Controls.Add(_rawStatusLabel);
        AddBodyRow(body, buttonRow, 44);

        var resultRow = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 56,
            AutoSize = false,
            ColumnCount = 4,
            Padding = new Padding(0)
        };
        resultRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        resultRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        resultRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        resultRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        resultRow.Controls.Add(CreateMetricField("识别码", out _rawSummaryLabel), 0, 0);
        resultRow.Controls.Add(CreateMetricField("最大间隔（天）", out _rawMaxGapLabel), 1, 0);
        resultRow.Controls.Add(CreateMetricField("首次100分天数", out _rawFirstHundredLabel), 2, 0);
        resultRow.Controls.Add(CreateMetricField("100 分日期数量", out _rawHundredCountLabel), 3, 0);
        AddBodyRow(body, resultRow, 56);
        var rawRecentRow = new FlowLayoutPanel
        {
            AutoSize = false,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        var rawRecentCaption = CreateCaptionLabel("自动查找最近一次满分日期（从今天往前）");
        _rawRecentSwitch = new AntdUI.Switch
        {
            AutoSize = false,
            Size = new Size(44, 22),
            Checked = true,
            Margin = new Padding(8, 0, 12, 0)
        };
        _rawRecentSwitch.CheckedChanged += async (_, _) => await SearchRawRecentHundredAsync();
        _rawRecentLabel = CreateValueLabel("未启用");
        rawRecentRow.Controls.Add(rawRecentCaption);
        rawRecentRow.Controls.Add(_rawRecentSwitch);
        rawRecentRow.Controls.Add(_rawRecentLabel);
        AddBodyRow(body, rawRecentRow, 34);
        AddBodyRow(body, CreateCaptionLabel("100 分日期列表（yyyy-MM-dd）"), 24);
        _rawDatesPanel = CreateDateListPanel();
        AddBodyRow(body, _rawDatesPanel, 170);

        card.Controls.Add(body);
        return card;
    }

    private Control CreateBenchmarkCard()
    {
        var card = CreateCardShell();
        var body = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 1,
            Padding = new Padding(0)
        };

        AddBodyRow(body, CreateCaptionLabel("固定随机生成 1,000,000 个识别码，完整计算 1780 天窗口，用于衡量真实 CPU 性能。"), 28);

        var buttonRow = new FlowLayoutPanel
        {
            AutoSize = false,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        _benchmarkButton = CreateButton("开始性能基准测试", BenchmarkAsync, primary: true);
        _cancelBenchmarkButton = CreateButton("取消基准测试", CancelBenchmark, danger: true);
        _cancelBenchmarkButton.Enabled = false;
        _showBenchmarkBestButton = CreateButton("查看测试最佳识别码", ShowBenchmarkBest);
        _showBenchmarkBestButton.Enabled = false;
        buttonRow.Controls.Add(_benchmarkButton);
        buttonRow.Controls.Add(_cancelBenchmarkButton);
        buttonRow.Controls.Add(_showBenchmarkBestButton);
        AddBodyRow(body, buttonRow, 44);

        _benchmarkStatusLabel = CreateValueLabel("点击“开始性能基准测试”以测量当前 CPU 性能。");
        AddBodyRow(body, _benchmarkStatusLabel, 28);
        _benchmarkResultLabel = CreateValueLabel("尚未运行基准测试。");
        _benchmarkResultLabel.TextMultiLine = true;
        AddBodyRow(body, _benchmarkResultLabel, 90);
        _benchmarkBestLabel = CreateValueLabel("");
        _benchmarkBestLabel.ForeColor = DesignTokens.Primary;
        _benchmarkBestLabel.TextMultiLine = true;
        AddBodyRow(body, _benchmarkBestLabel, 30);

        card.Controls.Add(body);
        return card;
    }

    // ==================== 控件工厂 ====================

    private static AntdUI.Button CreateButton(string text, Action onClick, bool primary = false, bool danger = false)
    {
        var button = new AntdUI.Button
        {
            Text = text,
            AutoSize = true,
            Radius = DesignTokens.RoundedMd,
            BorderWidth = primary ? 0 : 1,
            Cursor = Cursors.Hand,
            Type = primary ? AntdUI.TTypeMini.Primary : danger ? AntdUI.TTypeMini.Error : AntdUI.TTypeMini.Default
        };
        ApplyButtonPalette(button, primary, danger);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static AntdUI.Button CreateButton(string text, Func<Task> onClick, bool primary = false, bool danger = false)
    {
        var button = new AntdUI.Button
        {
            Text = text,
            AutoSize = true,
            Radius = DesignTokens.RoundedMd,
            BorderWidth = primary ? 0 : 1,
            Cursor = Cursors.Hand,
            Type = primary ? AntdUI.TTypeMini.Primary : danger ? AntdUI.TTypeMini.Error : AntdUI.TTypeMini.Default
        };
        ApplyButtonPalette(button, primary, danger);
        button.Click += async (_, _) => await onClick();
        return button;
    }

    private static void ApplyButtonPalette(AntdUI.Button button, bool primary, bool danger)
    {
        if (primary)
        {
            // Ant Design primary 按钮：#1677FF。
            button.BackColor = DesignTokens.PrimaryAction;
            button.BackHover = DesignTokens.PrimaryActionHover;
            button.BackActive = DesignTokens.PrimaryActionActive;
            button.ForeColor = DesignTokens.OnPrimary;
            button.ForeHover = DesignTokens.OnPrimary;
            button.ForeActive = DesignTokens.OnPrimary;
        }
        else if (danger)
        {
            // DESIGN.md components.button-danger：浅底深红文字。
            button.ForeColor = DesignTokens.DangerStrong;
        }
    }

    private static AntdUI.Input CreateInput(string placeholder, string? text = null, bool readOnly = false)
    {
        var input = new AntdUI.Input
        {
            PlaceholderText = placeholder,
            Radius = DesignTokens.RoundedMd,
            ReadOnly = readOnly,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight,
            AutoSize = false
        };
        if (!string.IsNullOrEmpty(text))
        {
            input.Text = text;
        }
        return input;
    }

    private AntdUI.Label CreateCaptionLabel(string text)
    {
        var label = new AntdUI.Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = DesignTokens.TextSecondaryColor(_isDark),
            Font = new Font(AppleTheme.FontFamily, 9F),
            Margin = new Padding(0)
        };
        _captionLabels.Add(label);
        return label;
    }
    private static AntdUI.Label CreateValueLabel(string text, bool right = false)
    {
        return new AntdUI.Label
        {
            Text = text,
            AutoSize = true,
            TextAlign = right ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
            Font = new Font(AppleTheme.FontFamily, 10F)
        };
    }

    private static TableLayoutPanel CreateFieldPanel()
    {
        var panel = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 56,
            AutoSize = false,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        // 输入框/日期选择器/分段按钮统一为 36px 高，严格对齐。
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.ControlHeight));
        return panel;
    }

    private Control CreateField(string caption, out AntdUI.Input input, string? defaultValue)
    {
        var panel = CreateFieldPanel();
        panel.Controls.Add(CreateCaptionLabel(caption), 0, 0);
        input = CreateInput(caption, defaultValue);
        panel.Controls.Add(input, 0, 1);
        return panel;
    }

    private Control CreateField(string caption, out AntdUI.DatePicker picker, DateTime? defaultValue)
    {
        var panel = CreateFieldPanel();
        panel.Controls.Add(CreateCaptionLabel(caption), 0, 0);
        picker = new AntdUI.DatePicker
        {
            Value = defaultValue ?? DateTime.Today,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight,
            Radius = DesignTokens.RoundedMd,
            Format = "yyyy-MM-dd"
        };
        panel.Controls.Add(picker, 0, 1);
        return panel;
    }

    private Control CreateField(string caption, out AntdUI.Segmented segmented, DateTime? _)
    {
        var panel = CreateFieldPanel();
        panel.Controls.Add(CreateCaptionLabel(caption), 0, 0);
        segmented = new AntdUI.Segmented
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight,
            Radius = DesignTokens.RoundedMd,
            Gap = 4
        };
        segmented.Items.Add(new AntdUI.SegmentedItem { Text = "最大间隔" });
        segmented.Items.Add(new AntdUI.SegmentedItem { Text = "距今最久" });
        segmented.SelectIndex = 0;
        panel.Controls.Add(segmented, 0, 1);
        return panel;
    }

    private Control CreateMetricField(string caption, out AntdUI.Label valueLabel)
    {
        var panel = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 56,
            AutoSize = false,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.ControlHeight));
        panel.Controls.Add(CreateCaptionLabel(caption), 0, 0);
        valueLabel = new AntdUI.Label
        {
            Text = "—",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font(AppleTheme.FontFamily, 14F, FontStyle.Bold),
            Margin = new Padding(0, 0, 12, 0)
        };
        panel.Controls.Add(valueLabel, 0, 1);
        return panel;
    }

    private static TableLayoutPanel CreateTwoColumnRow(Control left, Control right)
    {
        var row = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 56,
            AutoSize = false,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Margin = new Padding(0, 0, 12, 0);
        right.Margin = new Padding(12, 0, 0, 0);
        row.Controls.Add(left, 0, 0);
        row.Controls.Add(right, 1, 0);
        return row;
    }

    private static TableLayoutPanel CreateTwoOrThreeColumnRow(Control c1, Control c2, Control c3)
    {
        var row = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Height = 56,
            AutoSize = false,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        c1.Margin = new Padding(0, 0, 8, 0);
        c2.Margin = new Padding(8, 0, 8, 0);
        c3.Margin = new Padding(8, 0, 0, 0);
        row.Controls.Add(c1, 0, 0);
        row.Controls.Add(c2, 1, 0);
        row.Controls.Add(c3, 2, 0);
        return row;
    }

    private static void AddBodyRow(TableLayoutPanel body, Control control, int height = 44)
    {
        body.RowCount++;
        // 每行下方统一留 8px，形成稳定的纵向节奏，避免内容挤在一起。
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, height + DesignTokens.SpacingSm));
        // 有明确高度/自动尺寸的控件用 Dock=Top，只横向铺满、不纵向拉伸；
        // 无固定高度的行容器才用 Fill，保证统一、可预测的高度。
        control.Dock = control.AutoSize || control.Height > 0
            ? DockStyle.Top
            : DockStyle.Fill;
        control.Margin = new Padding(0);
        body.Controls.Add(control, 0, body.RowCount - 1);
    }

    private FlowLayoutPanel CreateDateListPanel()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Height = DesignTokens.DateListHeight,
            Padding = new Padding(8),
            BackColor = DesignTokens.ScrollAreaColor(_isDark)
        };
    }

    private void UpdateScrollAreaColors()
    {
        if (_resultDatesPanel is not null)
        {
            _resultDatesPanel.BackColor = DesignTokens.ScrollAreaColor(_isDark);
        }

        if (_rawDatesPanel is not null)
        {
            _rawDatesPanel.BackColor = DesignTokens.ScrollAreaColor(_isDark);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_sections.Count > 0)
        {
            ShowSection(Math.Max(0, _activeSection));
        }

        UpdateScrollAreaColors();

        // 启动欢迎动画：复用页面切换的“轻量滑入”，不做整窗 Opacity 淡入，
        // 避免把复杂 UI 切到 Layered Window 导致掉帧/卡顿。
        if (!_startupEntrancePlayed && _activeSection >= 0 && _activeSection < _sections.Count)
        {
            _startupEntrancePlayed = true;
            StartSectionAnimation(-1, _activeSection);
        }

        // 性能监控从窗口显示起持续运行，不再只是计算时的“装饰品”。
        _lastPerfSampleTime = DateTime.UtcNow;
        _lastPerfCpu = Process.GetCurrentProcess().TotalProcessorTime;
        _perfTimer.Tick += OnPerfTimerTick;
        _perfTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_sectionAnimTimer is not null)
        {
            _sectionAnimTimer.Stop();
            _sectionAnimTimer.Tick -= OnSectionAnimationTick;
            _sectionAnimTimer.Dispose();
            _sectionAnimTimer = null;
        }

        _perfTimer.Stop();
        _perfTimer.Tick -= OnPerfTimerTick;
        _perfTimer.Dispose();

        if (_notifyHideTimer is not null)
        {
            _notifyHideTimer.Stop();
            _notifyHideTimer.Dispose();
            _notifyHideTimer = null;
        }

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        StopPerformanceMonitor();
        base.OnFormClosed(e);
    }

    // ==================== 主题 ====================

    private void ApplyTheme()
    {
        _isDark = AntdUI.Config.IsDark;
        BackColor = DesignTokens.WindowBackground(_isDark);
        ForeColor = DesignTokens.WindowForeground(_isDark);
        Dark = _isDark;
        if (_root is not null)
        {
            _root.BackColor = DesignTokens.WindowBackground(_isDark);
        }
        if (_centerLayout is not null)
        {
            _centerLayout.BackColor = DesignTokens.WindowBackground(_isDark);
        }
        _statusLabel.ForeColor = DesignTokens.TextSecondaryColor(_isDark);
        foreach (var caption in _captionLabels)
        {
            caption.ForeColor = DesignTokens.TextSecondaryColor(_isDark);
        }
        if (_sidebar is not null)
        {
            _sidebar.Back = DesignTokens.SidebarColor(_isDark);
            _sidebar.BackColor = DesignTokens.SidebarColor(_isDark);
        }
        if (_rightPanel is not null)
        {
            _rightPanel.Back = Color.Transparent;
            _rightPanel.BackColor = Color.Transparent;
        }
        if (_bottomBar is not null)
        {
            _bottomBar.BackColor = Color.Transparent;
        }
        if (_rightPerfCard is not null)
        {
            _rightPerfCard.Back = Color.Transparent;
            _rightPerfCard.BackColor = Color.Transparent;
        }
        UpdateSectionCardTheme();
        UpdateScrollAreaColors();
        UpdateActionButtonsTheme();
        SyncNavSelection();
    }

    private void ToggleTheme()
    {
        _isDark = !_isDark;
        AntdUI.Config.IsDark = _isDark;
        Dark = _isDark;
        BackColor = DesignTokens.WindowBackground(_isDark);
        ForeColor = DesignTokens.WindowForeground(_isDark);
        if (_root is not null)
        {
            _root.BackColor = DesignTokens.WindowBackground(_isDark);
        }
        if (_centerLayout is not null)
        {
            _centerLayout.BackColor = DesignTokens.WindowBackground(_isDark);
        }
        _statusLabel.ForeColor = DesignTokens.TextSecondaryColor(_isDark);
        foreach (var caption in _captionLabels)
        {
            caption.ForeColor = DesignTokens.TextSecondaryColor(_isDark);
        }
        _statusLabel.Text = _isDark ? "已切换到深色模式。" : "已切换到浅色模式。";
        if (_sidebar is not null)
        {
            _sidebar.Back = DesignTokens.SidebarColor(_isDark);
            _sidebar.BackColor = DesignTokens.SidebarColor(_isDark);
        }
        if (_rightPanel is not null)
        {
            _rightPanel.Back = Color.Transparent;
            _rightPanel.BackColor = Color.Transparent;
        }
        if (_bottomBar is not null)
        {
            _bottomBar.BackColor = Color.Transparent;
        }
        if (_rightPerfCard is not null)
        {
            _rightPerfCard.Back = Color.Transparent;
            _rightPerfCard.BackColor = Color.Transparent;
        }
        UpdateSectionCardTheme();
        UpdateScrollAreaColors();
        UpdateActionButtonsTheme();
        SyncNavSelection();
    }

    private void UpdateActionButtonsTheme()
    {
        if (_cancelButton is null) return;
        _cancelButton.BackColor = DesignTokens.SurfaceColor(_isDark);
        _cancelButton.BackHover = DesignTokens.SurfaceHoverColor(_isDark);
        _cancelButton.BackActive = DesignTokens.SurfaceHoverColor(_isDark);
        _cancelButton.ForeColor = DesignTokens.DangerStrong;
        _cancelButton.ForeHover = DesignTokens.DangerStrong;
        _cancelButton.ForeActive = DesignTokens.DangerStrong;
    }
    private void UpdateSectionCardTheme()
    {
        foreach (var section in _sections)
        {
            if (section is AntdUI.Panel panel)
            {
                panel.Back = Color.Transparent;
                panel.BackColor = Color.Transparent;
            }
        }
    }
    // ==================== UI 更新 ====================

    private void UpdateSourceVisibility()
    {
        var isFile = _sourceSegmented.SelectIndex == 1;
        _filePanel.Visible = isFile;
        _countField.Visible = !isFile;

        // 文件模式下把数量列收缩为 0，让“窗口天数 / K 值”自动补位对齐。
        if (isFile)
        {
            _paramRow.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 0);
            _paramRow.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 50);
            _paramRow.ColumnStyles[2] = new ColumnStyle(SizeType.Percent, 50);
            _countField.Margin = new Padding(0);
        }
        else
        {
            _paramRow.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 33.33F);
            _paramRow.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 33.33F);
            _paramRow.ColumnStyles[2] = new ColumnStyle(SizeType.Percent, 33.33F);
            _countField.Margin = new Padding(0, 0, 8, 0);
        }

        _paramRow.PerformLayout();
    }

    private void UpdateModeSubtitle()
    {
        _pageHeader.SubText = SelectedMode == ScanMode.MaxGap
            ? "寻找 100 分日期最大间隔最大的识别码"
            : "寻找第一个 100 分日期出现最晚的识别码";
    }

    private void RefreshResultItems()
    {
        if (_resultSelect is null || _resultSortField is null || _resultSortDirection is null || _resultFilterInput is null) return;

        _updatingResultList = true;
        try
        {
            var filter = (_resultFilterInput.Text ?? string.Empty).Trim();
            var selectedId = _selectedResult?.Id;

            IEnumerable<TopKResult> query = _topResults;
            if (filter.Length > 0)
            {
                query = query.Where(x => x.Id.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            var field = _resultSortField.SelectedIndex;
            var desc = _resultSortDirection.SelectIndex == 1;
            switch (field)
            {
                case 0:
                    query = query.OrderBy(x => x.DiscoveredAt);
                    break;
                case 1:
                    query = desc
                        ? query.OrderByDescending(x => x.KeyMetric).ThenBy(x => x.DiscoveredAt)
                        : query.OrderBy(x => x.KeyMetric).ThenBy(x => x.DiscoveredAt);
                    break;
                case 2:
                    query = desc
                        ? query.OrderByDescending(x => x.HundredCount).ThenByDescending(x => x.KeyMetric)
                        : query.OrderBy(x => x.HundredCount).ThenByDescending(x => x.KeyMetric);
                    break;
                case 3:
                    query = desc
                        ? query.OrderByDescending(x => x.Id, StringComparer.OrdinalIgnoreCase)
                        : query.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase);
                    break;
            }

            var list = query.ToList();
            _resultSelect.Items.Clear();
            foreach (var item in list)
            {
                _resultSelect.Items.Add(new AntdUI.SelectItem(item.DisplayText, item));
            }

            if (_resultSelect.Items.Count > 0)
            {
                var index = 0;
                if (selectedId is not null)
                {
                    var found = list.FindIndex(x => x.Id == selectedId);
                    if (found >= 0) index = found;
                }

                _resultSelect.SelectedIndex = index;
            }
            else
            {
                _resultSelect.SelectedIndex = -1;
            }
        }
        finally
        {
            _updatingResultList = false;
        }

        UpdateSelectedResultDetail();
    }
    private void UpdateSelectedResultDetail()
    {
        var selected = GetSelectedResult();
        _selectedResult = selected;
        if (selected is null)
        {
            _resultIdInput.Text = string.Empty;
            _resultMetricLabel.Text = "0";
            _resultHundredCountLabel.Text = "0";
            _resultFirstDateLabel.Text = string.Empty;
            _resultDatesPanel.Controls.Clear();
            _copyButton.Enabled = false;
            _fillRawButton.Enabled = false;
            return;
        }

        _resultIdInput.Text = selected.Id;
        _resultMetricLabel.Text = selected.KeyMetric.ToString("N0");
        _resultHundredCountLabel.Text = selected.HundredCount.ToString("N0");
        _resultFirstDateLabel.Text = selected.Mode == ScanMode.First100Date
            ? selected.First100Date?.ToString("yyyy-MM-dd dddd") ?? string.Empty
            : selected.HundredDates.Count > 0
                ? selected.HundredDates[0].ToString("yyyy-MM-dd dddd")
                : string.Empty;
        _copyButton.Enabled = !_isBusy;
        _fillRawButton.Enabled = !_isBusy && !_isRawBusy;

        _resultDatesPanel.Controls.Clear();
        foreach (var date in selected.HundredDates)
        {
            _resultDatesPanel.Controls.Add(new AntdUI.Label
            {
                Text = date.ToString("yyyy-MM-dd dddd"),
                AutoSize = true,
                Font = new Font(AppleTheme.FontFamily ?? Font.FontFamily, 9.5F),
                Margin = new Padding(0, 2, 16, 2)
            });
        }

        if (_resultRecentSwitch is not null && _resultRecentSwitch.Checked)
        {
            _ = SearchResultRecentHundredAsync();
        }
    }

    private TopKResult? GetSelectedResult()
    {
        if (_resultSelect.SelectedIndex < 0 || _resultSelect.SelectedIndex >= _resultSelect.Items.Count)
        {
            return null;
        }

        if (_resultSelect.Items[_resultSelect.SelectedIndex] is AntdUI.SelectItem item)
        {
            return item.Tag as TopKResult;
        }

        return null;
    }

    private async Task SearchResultRecentHundredAsync()
    {
        if (_resultRecentSwitch is null || !_resultRecentSwitch.Checked || string.IsNullOrEmpty(_resultIdInput.Text))
        {
            if (_resultRecentLabel is not null) _resultRecentLabel.Text = "未启用";
            return;
        }

        if (!int.TryParse(_daysInput.Text, out var days) || days <= 0) days = 1780;
        _resultRecentLabel.Text = "查找中…";
        try
        {
            var date = await FindMostRecentHundredDateAsync(_resultIdInput.Text.Trim(), days);
            _resultRecentLabel.Text = date ?? "窗口内未找到";
            if (date is not null) AddRecentDateToPanel(_resultDatesPanel, date);
        }
        catch (Exception ex)
        {
            _resultRecentLabel.Text = "查找失败：" + ex.Message;
        }
    }

    private async Task SearchRawRecentHundredAsync()
    {
        if (_rawRecentSwitch is null || !_rawRecentSwitch.Checked || string.IsNullOrEmpty(_rawIdInput.Text))
        {
            if (_rawRecentLabel is not null) _rawRecentLabel.Text = "未启用";
            return;
        }

        if (!int.TryParse(_rawDaysInput.Text, out var days) || days <= 0) days = 1780;
        _rawRecentLabel.Text = "查找中…";
        try
        {
            var canonical = CanonicalizeForRawVerify(_rawIdInput.Text.Trim());
            var date = await FindMostRecentHundredDateAsync(canonical, days);
            _rawRecentLabel.Text = date ?? "窗口内未找到";
            if (date is not null) AddRecentDateToPanel(_rawDatesPanel, date);
        }
        catch (Exception ex)
        {
            _rawRecentLabel.Text = "查找失败：" + ex.Message;
        }
    }

    private static void AddRecentDateToPanel(FlowLayoutPanel panel, string date)
    {
        if (panel is null) return;
        var exists = panel.Controls.OfType<AntdUI.Label>().Any(x => x.Text is not null && x.Text.Contains(date, StringComparison.Ordinal));
        if (exists) return;

        var label = new AntdUI.Label
        {
            Text = $"最近满分：{date}",
            AutoSize = true,
            Font = new Font(AppleTheme.FontFamily, 9.5F),
            ForeColor = DesignTokens.Primary,
            Margin = new Padding(0, 2, 16, 2)
        };
        panel.Controls.Add(label);
        panel.Controls.SetChildIndex(label, 0);
    }
    private static Task<string?> FindMostRecentHundredDateAsync(string id, int lookbackDays)
    {
        return Task.Run(() =>
        {
            if (lookbackDays <= 0) lookbackDays = 1780;
            var today = DateTime.Today.Date;
            var start = today.AddDays(-(lookbackDays - 1));
            var result = RawVerifier.CheckId(id, start, lookbackDays);
            return result.HundredDates.Count > 0 ? result.HundredDates[^1] : null;
        });
    }
    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        UpdateOperationEnabled();
    }

    private void UpdateOperationEnabled()
    {
        var locked = _isBusy || _isRawBusy || _isBenchmarkRunning;

        if (_sourceSegmented is not null) _sourceSegmented.Enabled = !locked;
        if (_countInput is not null) _countInput.Enabled = !locked;
        if (_daysInput is not null) _daysInput.Enabled = !locked;
        if (_kInput is not null) _kInput.Enabled = !locked;
        if (_fileInput is not null) _fileInput.Enabled = !locked;
        if (_browseButton is not null) _browseButton.Enabled = !locked;
        if (_startDatePicker is not null) _startDatePicker.Enabled = !locked;
        if (_modeSegmented is not null) _modeSegmented.Enabled = !locked;
        if (_rawIdInput is not null) _rawIdInput.Enabled = !locked;
        if (_rawStartDatePicker is not null) _rawStartDatePicker.Enabled = !locked;
        if (_rawDaysInput is not null) _rawDaysInput.Enabled = !locked;

        // Loading 使用 AntdUI 自带的按钮加载动画；执行中的按钮保持可点击外观，
        // 但事件入口都已有 busy 防重入，所以不会真正重复启动。
        if (_startButton is not null)
        {
            _startButton.Loading = _isBusy;
            _startButton.Enabled = !locked || _isBusy;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Enabled = _isBusy;
        }

        if (_rawComputeButton is not null)
        {
            _rawComputeButton.Loading = _isRawBusy;
            _rawComputeButton.Enabled = !locked || _isRawBusy;
        }

        if (_fillRawButton is not null)
        {
            _fillRawButton.Enabled = !locked && _selectedResult is not null;
        }

        if (_benchmarkButton is not null)
        {
            _benchmarkButton.Loading = _isBenchmarkRunning;
            _benchmarkButton.Enabled = !locked || _isBenchmarkRunning;
        }

        if (_copyButton is not null)
        {
            _copyButton.Enabled = !locked && _selectedResult is not null;
        }
    }
    // ==================== 计算逻辑 ====================

    private async Task StartAsync()
    {
        if (_isBusy)
        {
            return;
        }

        if (!TryParseInputs(out var count, out var days, out var k))
        {
            return;
        }

        var mode = SelectedMode;
        var info = new DateRangeInfo(_startDatePicker.Value?.Date ?? DateTime.Today, days);
        IEnumerable<string> ids;
        long? totalCount;
        Func<string, string?>? idNormalizer = null;

        if (CurrentSource == SourceMode.Random)
        {
            var generator = new RandomIdGenerator();
            ids = generator.TakeLong(count);
            totalCount = count;
        }
        else
        {
            if (!File.Exists(_fileInput.Text.Trim()))
            {
                _statusLabel.Text = "请选择有效的识别码文件。";
                return;
            }

            ids = FileIdSource.ReadLines(_fileInput.Text.Trim());
            totalCount = null;
            idNormalizer = line => IdFormat.TryNormalize(line, out var normalized) ? normalized : null;
        }

        _cts = new CancellationTokenSource();
        StartPerformanceMonitor(totalCount, _cts.Token);

        ClearResult();
        _progress.Value = 0F;
        _progress.Loading = totalCount is null;
        _progress.Text = string.Empty;
        _statusLabel.Text = mode == ScanMode.MaxGap ? "正在计算最大间隔 Top-K…" : "正在计算距今最久 Top-K…";
        _processedLabel.Text = "0";
        _elapsedLabel.Text = "00:00:00";
        _processedLabel.Visible = true;
        _elapsedLabel.Visible = true;
        SetBusy(true);

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
            ShowSection(2); // 自动跳到“分析结果”
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "已取消，显示当前已找到的最佳结果。";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"错误：{ex.Message}";
        }
        finally
        {
            StopPerformanceMonitor();
            SetBusy(false);
            _progress.Loading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
        _statusLabel.Text = "正在取消…";
    }

    private bool TryParseInputs(out long count, out int days, out int k)
    {
        count = 0;
        days = 0;
        k = 0;

        if (CurrentSource == SourceMode.Random)
        {
            if (!CountParser.TryParse(_countInput.Text, out count))
            {
                _statusLabel.Text = "识别码数量格式无效。支持整数或科学计数法，如 1e10。";
                return false;
            }

            if (count <= 0 || count > MaxCount)
            {
                _statusLabel.Text = $"识别码数量必须介于 1 和 {MaxCount:N0} 之间。";
                return false;
            }
        }

        if (!int.TryParse(_kInput.Text, out k) || k < MinK || k > MaxK)
        {
            _statusLabel.Text = $"K 值必须介于 {MinK} 和 {MaxK} 之间。";
            return false;
        }

        if (!int.TryParse(_daysInput.Text, out days) || days <= 0 || days > MaxWindowDays)
        {
            _statusLabel.Text = $"窗口天数必须介于 1 和 {MaxWindowDays:N0} 之间。";
            return false;
        }

        return true;
    }

    private SourceMode CurrentSource => _sourceSegmented.SelectIndex == 0 ? SourceMode.Random : SourceMode.File;
    private ScanMode SelectedMode => _modeSegmented.SelectIndex == 0 ? ScanMode.MaxGap : ScanMode.First100Date;

    private void OnProgress(RpProgressInfo info)
    {
        UpdatePerfProcessed(info.ProcessedCount);

        if (info.TotalCount is long total)
        {
            _progress.Value = total > 0 ? (float)((double)info.ProcessedCount / total) : 0F;
            _progress.Loading = false;
            _processedLabel.Text = $"{info.ProcessedCount:N0} / {total:N0}";
        }
        else
        {
            _processedLabel.Text = $"{info.ProcessedCount:N0}";
        }

        _elapsedLabel.Text = _perfStopwatch.IsRunning ? _perfStopwatch.Elapsed.ToString(@"hh\:mm\:ss") : "00:00:00";

        var currentBestText = info.CurrentBestMetric >= 0 && !string.IsNullOrEmpty(info.CurrentBestId)
            ? SelectedMode == ScanMode.MaxGap
                ? $"{info.CurrentBestId}：{info.CurrentBestMetric} 天"
                : $"{info.CurrentBestId}：首次100分第 {info.CurrentBestMetric} 天"
            : "暂无";

        _rightProcessedLabel.Text = info.ProcessedCount.ToString("N0");
        _rightBestLabel.Text = currentBestText;
    }
    private void ApplyResult(RpProcessingResult result)
    {
        _topResults = result.TopResults.OrderBy(x => x.DiscoveredAt).ToList();
        _selectedResult = null;
        RefreshResultItems();

        _progress.Value = 1F;
        _progress.Loading = false;
        _processedLabel.Text = result.ProcessedCount.ToString("N0");
        _elapsedLabel.Text = result.Elapsed.ToString(@"hh\:mm\:ss");
        _rightProcessedLabel.Text = result.ProcessedCount.ToString("N0");
        _rightBestLabel.Text = result.Best?.DisplayText ?? "暂无";

        _statusLabel.Text = result.IsCancelled
            ? "已取消。当前显示的是取消前找到的最佳结果，计算未完成。"
            : result.ProcessedCount > 0 && result.ProcessedCount == result.InvalidCount
                ? "计算完成。文件中没有有效的识别码。"
                : "计算完成。";

        if (!result.IsCancelled && result.ProcessedCount > 0 && result.ProcessedCount != result.InvalidCount)
        {
            ShowInAppMessage("计算完成，已切换到“分析结果”。");
            ShowWindowsNotification("今日人品间隔分析器", "计算完成，已切换到“分析结果”。");
        }
    }
    private void ClearResult()
    {
        _topResults = new List<TopKResult>();
        _selectedResult = null;
        if (_resultFilterInput is not null) _resultFilterInput.Text = string.Empty;
        if (_resultSortField is not null) _resultSortField.SelectedIndex = 1;
        if (_resultSortDirection is not null) _resultSortDirection.SelectIndex = 1;
        RefreshResultItems();
        _resultIdInput.Text = string.Empty;
        _resultMetricLabel.Text = "0";
        _resultHundredCountLabel.Text = "0";
        _resultFirstDateLabel.Text = string.Empty;
        _resultDatesPanel.Controls.Clear();
        _copyButton.Enabled = false;
        _rightBestLabel.Text = "暂无";
        _rightProcessedLabel.Text = "0";
        _rightSpeedLabel.Text = "—";
        _rightEtaLabel.Text = "—";
    }
    // ==================== 原始计算 ====================

    private async Task RawComputeAsync()
    {
        if (_isRawBusy || _isBusy)
        {
            return;
        }

        var idText = (_rawIdInput.Text ?? string.Empty).Trim();
        if (idText.Length == 0)
        {
            _rawStatusLabel.Text = "请先输入要验算的识别码。";
            return;
        }

        string canonicalId;
        try
        {
            canonicalId = CanonicalizeForRawVerify(idText);
        }
        catch (ArgumentException ex)
        {
            _rawStatusLabel.Text = ex.Message;
            return;
        }

        if (!int.TryParse(_rawDaysInput.Text, out var days) || days <= 0 || days > MaxWindowDays)
        {
            _rawStatusLabel.Text = $"窗口天数必须介于 1 和 {MaxWindowDays:N0} 之间。";
            return;
        }

        var startDate = _rawStartDatePicker.Value?.Date ?? DateTime.Today;
        _isRawBusy = true;
        _rawStatusLabel.Text = "正在按原始算法逐日验算…";
        _rawSummaryLabel.Text = "—";
        _rawMaxGapLabel.Text = "—";
        _rawFirstHundredLabel.Text = "—";
        _rawHundredCountLabel.Text = "—";
        _rawDatesPanel.Controls.Clear();
        SetBusy(_isBusy);

        try
        {
            var result = await Task.Run(() => RawVerifier.CheckId(canonicalId, startDate, days));

            _rawSummaryLabel.Text = result.Id;
            _rawMaxGapLabel.Text = result.MaxGap.ToString("N0");
            _rawFirstHundredLabel.Text = result.FirstHundredIndex >= 0
                ? (result.FirstHundredIndex + 1).ToString()
                : "无";
            _rawHundredCountLabel.Text = result.HundredCount.ToString("N0");
            _rawDatesPanel.Controls.Clear();
            foreach (var d in result.HundredDates)
            {
                _rawDatesPanel.Controls.Add(new AntdUI.Label
                {
                    Text = d,
                    AutoSize = true,
                    Font = new Font(AppleTheme.FontFamily ?? Font.FontFamily, 9.5F),
                    Margin = new Padding(0, 2, 16, 2)
                });
            }

            _rawStatusLabel.Text = $"原始计算完成。窗口 {days:N0} 天内出现 100 分 {result.HundredCount:N0} 次，最大间隔 {result.MaxGap:N0} 天。";
            ShowInAppMessage("原始计算完成。");
            ShowWindowsNotification("今日人品间隔分析器", "原始计算完成。");
            if (_rawRecentSwitch is not null && _rawRecentSwitch.Checked) _ = SearchRawRecentHundredAsync();
        }
        catch (Exception ex)
        {
            _rawStatusLabel.Text = $"错误：{ex.Message}";
        }
        finally
        {
            _isRawBusy = false;
            SetBusy(_isBusy);
        }
    }

    private void FillRawFromBest()
    {
        var best = _selectedResult;
        if (best is null)
        {
            return;
        }

        _rawIdInput.Text = best.Id;
        _rawStartDatePicker.Value = _startDatePicker.Value?.Date ?? DateTime.Today;
        if (int.TryParse(_daysInput.Text, out var d) && d > 0)
        {
            _rawDaysInput.Text = _daysInput.Text;
        }

        _rawStatusLabel.Text = $"已从主结果填入：{best.Id}。点击“原始计算”开始验算。";
    }

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

    // ==================== 性能监控与基准测试 ====================

    private void StartPerformanceMonitor(long? totalCount, CancellationToken ct)
    {
        StopPerformanceMonitor();

        _perfTotal = totalCount;
        _perfProcessed = 0;
        _lastPerfProcessed = 0;
        _lastPerfSampleTime = DateTime.UtcNow;
        _lastPerfCpu = Process.GetCurrentProcess().TotalProcessorTime;
        _perfStopwatch.Restart();
    }

    private void StopPerformanceMonitor()
    {
        _perfLinkedCts?.Cancel();
        _perfLinkedCts?.Dispose();
        _perfLinkedCts = null;

        _perfCts?.Cancel();
        _perfCts?.Dispose();
        _perfCts = null;

        // 任务结束后回到空闲监控：速率/ETA 显示占位符，运行时间继续走应用级计时。
        _perfTotal = null;
        _perfProcessed = 0;
        _lastPerfProcessed = 0;
    }

    private void UpdatePerfProcessed(long processed)
    {
        Interlocked.Exchange(ref _perfProcessed, processed);
    }

    /// <summary>每秒刷新右侧“性能监控”：CPU/内存/运行时间/实时速率全部真实取数。</summary>
    private void OnPerfTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var wall = now - _lastPerfSampleTime;
        var cpuDelta = cpu - _lastPerfCpu;

        var cpuPercent = wall.TotalSeconds > 0
            ? Math.Max(0, Math.Min(100, cpuDelta.TotalSeconds / wall.TotalSeconds / Environment.ProcessorCount * 100.0))
            : 0.0;
        _lastPerfSampleTime = now;
        _lastPerfCpu = cpu;

        // 系统总 CPU 使用率（包含所有进程）
        double systemCpuPercent = 0;
        if (GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
        {
            var idleNew = ToUInt64(idleFt);
            var kernelNew = ToUInt64(kernelFt);
            var userNew = ToUInt64(userFt);
            if (_hasSystemCpuSample)
            {
                var totalDelta = (kernelNew - _lastSystemKernel) + (userNew - _lastSystemUser);
                var idleDelta = idleNew - _lastSystemIdle;
                if (totalDelta > 0)
                {
                    systemCpuPercent = Math.Max(0, Math.Min(100, (1.0 - (double)idleDelta / totalDelta) * 100.0));
                }
            }

            _lastSystemIdle = idleNew;
            _lastSystemKernel = kernelNew;
            _lastSystemUser = userNew;
            _hasSystemCpuSample = true;
        }

        var memoryMb = process.WorkingSet64 / 1024.0 / 1024.0;
        GetPhysicalMemoryMb(out var totalMemoryMb, out var availableMemoryMb);
        var usedMemoryMb = Math.Max(0, totalMemoryMb - availableMemoryMb);
        var memoryPercent = totalMemoryMb > 0 ? memoryMb / totalMemoryMb * 100.0 : 0.0;
        var otherMemoryPercent = totalMemoryMb > 0 ? Math.Max(0, usedMemoryMb - memoryMb) / totalMemoryMb * 100.0 : 0.0;

        var otherCpuPercent = Math.Max(0, systemCpuPercent - cpuPercent);
        var processed = Interlocked.Read(ref _perfProcessed);
        var speed = wall.TotalSeconds > 0
            ? (processed - _lastPerfProcessed) / wall.TotalSeconds
            : 0.0;
        _lastPerfProcessed = processed;

        // 空闲时“已运行”显示 00:00:00；只有正在计算/基准测试时才显示任务耗时。
        var active = _perfTotal is long total && total > 0;
        var elapsed = active ? _perfStopwatch.Elapsed : TimeSpan.Zero;

        string speedText = "—";
        string etaText = "—";
        if (active)
        {
            speedText = $"{speed:N0} 条/秒";
            if (_perfTotal is long total2 && speed > 0)
            {
                var etaSeconds = (total2 - processed) / speed;
                etaText = TimeSpan.FromSeconds(etaSeconds).ToString(@"hh\:mm\:ss");
            }
        }

        _perfCpuLabel.Text = $"本 {cpuPercent:F0}% · 其他 {otherCpuPercent:F0}%";
        _perfMemoryLabel.Text = $"本 {memoryPercent:F1}% · 其他 {otherMemoryPercent:F1}%";
        _perfElapsedLabel.Text = active ? elapsed.ToString(@"hh\:mm\:ss") : "00:00:00";
        _perfSpeedLabel.Text = speedText;
        _perfEtaLabel.Text = etaText;
        _rightSpeedLabel.Text = speedText;
        _rightEtaLabel.Text = etaText;

        if (_perfCpuBar is not null)
        {
            var used = (float)Math.Max(0, Math.Min(1, systemCpuPercent / 100.0));
            var otherCum = (float)Math.Max(0, Math.Min(used, otherCpuPercent / 100.0));
            _perfCpuBar.Value = used;
            _perfCpuBar.Segments = new[]
            {
                new AntdUI.ProgressSegment { Value = otherCum, Fill = Color.FromArgb(0, 82, 204) },
                new AntdUI.ProgressSegment { Value = used, Fill = Color.FromArgb(105, 177, 255) }
            };
        }

        if (_perfMemoryBar is not null)
        {
            var used = totalMemoryMb > 0 ? (float)Math.Max(0, Math.Min(1, usedMemoryMb / totalMemoryMb)) : 0F;
            var otherCum = (float)Math.Max(0, Math.Min(used, otherMemoryPercent / 100.0));
            _perfMemoryBar.Value = used;
            _perfMemoryBar.Segments = new[]
            {
                new AntdUI.ProgressSegment { Value = otherCum, Fill = Color.FromArgb(0, 82, 204) },
                new AntdUI.ProgressSegment { Value = used, Fill = Color.FromArgb(105, 177, 255) }
            };
        }
    }
    private async Task PerformanceMonitorLoopAsync(CancellationToken ct, IProgress<PerfSnapshot> progress)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var now = DateTime.UtcNow;
                var process = Process.GetCurrentProcess();
                var cpu = process.TotalProcessorTime;
                var wall = now - _lastPerfSampleTime;
                var cpuDelta = cpu - _lastPerfCpu;

                var cpuPercent = wall.TotalSeconds > 0
                    ? cpuDelta.TotalSeconds / wall.TotalSeconds / Environment.ProcessorCount * 100.0
                    : 0.0;
                var memoryMb = process.WorkingSet64 / 1024.0 / 1024.0;
                var processed = Interlocked.Read(ref _perfProcessed);
                var speed = wall.TotalSeconds > 0
                    ? (processed - _lastPerfProcessed) / wall.TotalSeconds
                    : 0.0;

                _lastPerfProcessed = processed;
                _lastPerfSampleTime = now;
                _lastPerfCpu = cpu;

                double? etaSeconds = null;
                if (_perfTotal is long total && speed > 0)
                {
                    etaSeconds = (total - processed) / speed;
                }

                progress.Report(new PerfSnapshot(cpuPercent, memoryMb, _perfStopwatch.Elapsed, speed, etaSeconds));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnPerfSnapshot(PerfSnapshot snapshot)
    {
        var totalMemoryMb = GetTotalPhysicalMemoryMb();
        var memoryPercent = totalMemoryMb > 0 ? snapshot.MemoryMb / totalMemoryMb * 100.0 : 0.0;

        _perfCpuLabel.Text = $"{snapshot.CpuPercent:F1}%";
        _perfMemoryLabel.Text = $"{snapshot.MemoryMb:F0} MB（{memoryPercent:F1}%）";
        _perfElapsedLabel.Text = snapshot.Elapsed.ToString(@"hh\:mm\:ss");
        _perfSpeedLabel.Text = $"{snapshot.Speed:N0} 条/秒";
        _perfEtaLabel.Text = snapshot.EtaSeconds is double eta
            ? TimeSpan.FromSeconds(eta).ToString(@"hh\:mm\:ss")
            : "—";
        _rightSpeedLabel.Text = _perfSpeedLabel.Text;
        _rightEtaLabel.Text = _perfEtaLabel.Text;

        if (_perfCpuBar is not null)
        {
            _perfCpuBar.Value = (float)Math.Max(0, Math.Min(1, snapshot.CpuPercent / 100.0));
            _perfMemoryBar.Value = (float)Math.Max(0, Math.Min(1, memoryPercent / 100.0));
        }
    }
    private async Task BenchmarkAsync()
    {
        if (_isBusy || _isBenchmarkRunning || _isRawBusy)
        {
            return;
        }

        const long benchmarkCount = 1_000_000L;
        const int benchmarkDays = 1780;

        var info = new DateRangeInfo(DateTime.Today, benchmarkDays);
        var ids = new RandomIdGenerator().TakeLong(benchmarkCount);

        _benchmarkCts = new CancellationTokenSource();
        _benchmarkStopwatch = Stopwatch.StartNew();
        _benchmarkBest = null;
        _benchmarkBestLabel.Text = string.Empty;
        _benchmarkResultLabel.Text = "基准测试运行中…";
        _benchmarkStatusLabel.Text = $"正在执行 {benchmarkCount:N0} 个识别码 × {benchmarkDays} 天完整计算…";
        _isBenchmarkRunning = true;
        UpdateOperationEnabled();
        _showBenchmarkBestButton.Enabled = false;
        _cancelBenchmarkButton.Enabled = true;
        _benchmarkButton.Enabled = false;
        _progress.Value = 0F;
        _progress.Loading = false;
        _processedLabel.Text = "0";
        _elapsedLabel.Text = "00:00:00";
        _statusLabel.Text = "性能基准测试运行中…";
        _processedLabel.Visible = true;
        _elapsedLabel.Visible = true;
        StartPerformanceMonitor(benchmarkCount, _benchmarkCts.Token);

        var cts = _benchmarkCts!;
        IProgress<long> progress = new Progress<long>(OnBenchmarkProgress);
        var processed = 0L;
        var bestLock = new object();
        TopKResult? best = null;
        long lastBenchmarkReportMs = 0;

        try
        {
            await Task.Run(
                () =>
                {
                    var options = new ParallelOptions
                    {
                        CancellationToken = cts.Token,
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };

                    Parallel.ForEach(ids, options, id =>
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        var scan = RpScanner.ScanCore(id, info);
                        if (scan.MaxGap > 0)
                        {
                            lock (bestLock)
                            {
                                if (best is null || scan.MaxGap > best.KeyMetric)
                                {
                                    best = new TopKResult
                                    {
                                        Id = id,
                                        Mode = ScanMode.MaxGap,
                                        KeyMetric = scan.MaxGap,
                                        HundredCount = scan.HundredCount,
                                        HundredDates = Array.Empty<DateTime>()
                                    };
                                }
                            }
                        }

                        var current = Interlocked.Increment(ref processed);
                        UpdatePerfProcessed(current);

                        var nowMs = _benchmarkStopwatch!.ElapsedMilliseconds;
                        var lastReport = Interlocked.Read(ref lastBenchmarkReportMs);
                        if (nowMs - lastReport >= 200 &&
                            Interlocked.CompareExchange(ref lastBenchmarkReportMs, nowMs, lastReport) == lastReport)
                        {
                            progress.Report(current);
                        }
                    });
                },
                CancellationToken.None);

            _benchmarkStopwatch.Stop();
            var elapsed = _benchmarkStopwatch.Elapsed;
            var avgSpeed = elapsed.TotalSeconds > 0 ? processed / elapsed.TotalSeconds : 0.0;
            var multiplier = avgSpeed / 90_000.0;
            var rating = GetPerformanceRating(avgSpeed);

            _benchmarkResultLabel.Text =
                $"总耗时：{FormatBenchmarkTime(elapsed)}\n" +
                $"平均处理速度：{avgSpeed:N0} 条/秒\n" +
                $"等效性能倍数：{multiplier:F2}x\n" +
                $"CPU 基准跑分：{avgSpeed:N0}\n" +
                $"性能评级：{rating}";
            _benchmarkStatusLabel.Text = "基准测试完成。";
            ShowInAppMessage("性能基准测试完成。");
            ShowWindowsNotification("今日人品间隔分析器", "性能基准测试完成。");
            _benchmarkBest = best;
            _showBenchmarkBestButton.Enabled = best is not null;
        }
        catch (OperationCanceledException)
        {
            _benchmarkStopwatch!.Stop();
            var elapsed = _benchmarkStopwatch.Elapsed;
            _benchmarkResultLabel.Text = "基准测试未完成";
            _benchmarkStatusLabel.Text = $"已取消。已处理 {processed:N0} 条，耗时 {FormatBenchmarkTime(elapsed)}。";
        }
        catch (Exception ex)
        {
            _benchmarkStopwatch?.Stop();
            _benchmarkResultLabel.Text = "基准测试出错";
            _benchmarkStatusLabel.Text = $"错误：{ex.Message}";
        }
        finally
        {
            StopPerformanceMonitor();
            _isBenchmarkRunning = false;
            UpdateOperationEnabled();
            _cancelBenchmarkButton.Enabled = false;
            _benchmarkButton.Enabled = !_isBusy && !_isRawBusy;
            _benchmarkCts?.Dispose();
            _benchmarkCts = null;
        }
    }

    private void OnBenchmarkProgress(long processed)
    {
        _benchmarkStatusLabel.Text = $"基准测试进行中：{processed:N0} / 1,000,000";
        _progress.Value = (float)Math.Max(0, Math.Min(1, processed / 1_000_000.0));
        _processedLabel.Text = $"{processed:N0} / 1,000,000";
        if (_benchmarkStopwatch is not null)
        {
            _elapsedLabel.Text = _benchmarkStopwatch.Elapsed.ToString(@"hh\:mm\:ss");
        }
    }
    private void CancelBenchmark()
    {
        _benchmarkCts?.Cancel();
        _benchmarkStatusLabel.Text = "正在取消基准测试…";
    }

    private void ShowBenchmarkBest()
    {
        if (_benchmarkBest is null)
        {
            return;
        }

        _benchmarkBestLabel.Text = $"最佳识别码：{_benchmarkBest.Id}（最大间隔 {_benchmarkBest.KeyMetric} 天，100分 {_benchmarkBest.HundredCount} 次）";
    }

    private static string FormatBenchmarkTime(TimeSpan time)
    {
        return time.ToString(@"hh\:mm\:ss\.fff");
    }

    private static string GetPerformanceRating(double speed)
    {
        if (speed < 90_000) return "较慢";
        if (speed < 150_000) return "普通";
        if (speed < 240_000) return "不错";
        if (speed < 360_000) return "很快";
        return "极快";
    }

    private sealed record PerfSnapshot(double CpuPercent, double MemoryMb, TimeSpan Elapsed, double Speed, double? EtaSeconds);

    // ==================== 剪贴板 / 文件 ====================

    private void CopyResult()
    {
        var selected = GetSelectedResult();
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
            $"首个100分日期: {_resultFirstDateLabel.Text}",
            "100分日期:",
        };
        lines.AddRange(_resultDatesPanel.Controls.OfType<AntdUI.Label>().Where(x => x.Text is not null).Select(x => x.Text!));
        Clipboard.SetText(string.Join(Environment.NewLine, lines));
        _statusLabel.Text = "结果已复制到剪贴板。";
        ShowInAppMessage("结果已复制到剪贴板。");
    }

    private void BrowseFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择识别码文件",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _fileInput.Text = dialog.FileName;
        }
    }
}
