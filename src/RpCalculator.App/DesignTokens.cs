using System.Drawing;

namespace RpCalculator.App;

/// <summary>
/// DESIGN.md 视觉规范的 C# 常量镜像。
/// 保持与仓库根目录 DESIGN.md front matter 完全一致；
/// 修改设计规范时应同步更新本文件并重新运行 design.md lint。
/// </summary>
internal static class DesignTokens
{
    // ==================== Colors（Ant Design 5 风格） ====================

    public static readonly Color Primary = FromHex("#1677FF");
    public static readonly Color PrimaryAction = FromHex("#1677FF");
    public static readonly Color PrimaryActionHover = FromHex("#4096FF");
    public static readonly Color PrimaryActionActive = FromHex("#0958D9");
    public static readonly Color OnPrimary = Color.White;

    public static readonly Color Background = FromHex("#F5F5F5");
    public static readonly Color BackgroundDark = FromHex("#141414");
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceDark = FromHex("#1F1F1F");
    public static readonly Color SurfaceHover = FromHex("#F0F0F0");
    public static readonly Color SurfaceHoverDark = FromHex("#2C2C2C");

    public static readonly Color TextPrimary = FromHex("#1F1F1F");
    public static readonly Color TextPrimaryDark = FromHex("#E6E6E6");
    public static readonly Color TextSecondary = FromHex("#595959");
    public static readonly Color TextSecondaryDark = FromHex("#A6A6A6");

    public static readonly Color Border = FromHex("#D9D9D9");
    public static readonly Color BorderDark = FromHex("#303030");

    public static readonly Color Danger = FromHex("#FF4D4F");
    public static readonly Color DangerStrong = FromHex("#D9363E");
    public static readonly Color OnDanger = Color.White;

    public static readonly Color WindowClose = FromHex("#FF5F57");
    public static readonly Color WindowMinimize = FromHex("#FFBD2E");
    public static readonly Color WindowMaximize = FromHex("#28C840");

    public static readonly Color Scrollbar = FromHex("#BFBFBF");
    public static readonly Color ScrollbarDark = FromHex("#424242");
    public static readonly Color ScrollArea = FromHex("#F0F0F0");
    public static readonly Color ScrollAreaDark = FromHex("#1A1A1A");
    public static readonly Color Sidebar = FromHex("#FAFAFA");
    public static readonly Color SidebarDark = FromHex("#1A1A1A");

    // ==================== 字体（与 DESIGN.md typography 对齐） ====================

    /// <summary>按优先级尝试的字体族；Windows 上通常命中 Microsoft YaHei UI。</summary>
    public static readonly string[] FontFamilies = { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "PingFang SC" };

    public static readonly string MonoFontFamily = "Consolas";

    // ==================== 形状（与 DESIGN.md rounded 对齐） ====================

    public const int RoundedNone = 0;
    public const int RoundedSm = 8;
    public const int RoundedMd = 12;
    public const int RoundedLg = 16;
    public const int RoundedFull = 9999;

    // ==================== 间距与尺寸（与 DESIGN.md spacing / components 对齐） ====================

    public const int SpacingXs = 4;
    public const int SpacingSm = 8;
    public const int SpacingMd = 12;
    public const int SpacingLg = 16;
    public const int SpacingXl = 24;
    public const int SpacingXxl = 32;

    public const int HeaderHeight = 64;
    public const int StatusBarHeight = 38;
    public const int ControlHeight = 36;
    public const int ProgressHeight = 10;
    public const int DateListHeight = 170;
    public const int CardInnerPadding = 24;
    public const int CardGap = 16;
    public const int WindowButtonWidth = 46;
    public const int WindowButtonHeight = 36;
    public const int TrafficLightSize = 14;
    public const int WindowRadius = 16;
    public const int WindowShadow = 20;
    public const int CardShadow = 12;
    public const int CardShadowOffsetY = 2;
    public const float CardShadowOpacity = 0.06F;

    // ==================== 主题辅助 ====================

    public static Color WindowBackground(bool dark) => dark ? BackgroundDark : Background;
    public static Color WindowForeground(bool dark) => dark ? TextPrimaryDark : TextPrimary;
    public static Color SurfaceColor(bool dark) => dark ? SurfaceDark : Surface;
    public static Color SurfaceHoverColor(bool dark) => dark ? SurfaceHoverDark : SurfaceHover;
    public static Color TextPrimaryColor(bool dark) => dark ? TextPrimaryDark : TextPrimary;
    public static Color TextSecondaryColor(bool dark) => dark ? TextSecondaryDark : TextSecondary;
    public static Color BorderColor(bool dark) => dark ? BorderDark : Border;
    public static Color ScrollAreaColor(bool dark) => dark ? ScrollAreaDark : ScrollArea;
    public static Color SidebarColor(bool dark) => dark ? SidebarDark : Sidebar;

    public static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static Color FromHex(string hex)
    {
        return ColorTranslator.FromHtml(hex);
    }
}
