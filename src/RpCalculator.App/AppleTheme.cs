namespace RpCalculator.App;

/// <summary>
/// Ant Design 风格全局主题与字体配置，取值来自 DESIGN.md 视觉规范。
/// 优先使用系统中已安装的微软雅黑 / Segoe UI，保证中文显示清晰稳定。
/// </summary>
internal static class AppleTheme
{
    private static FontFamily? _fontFamily;

    public static FontFamily FontFamily => _fontFamily ?? SystemFonts.DefaultFont.FontFamily;

    /// <summary>设置 AntdUI 全局配置与字体。</summary>
    public static void ApplyGlobal()
    {
        TryLoadFont();

        // 明确开启 AntdUI 原生动画（悬停、进度条、弹出层等），保证后续动效一致可用。
        AntdUI.Config.Animation = true;
    }

    private static void TryLoadFont()
    {
        foreach (var name in DesignTokens.FontFamilies)
        {
            try
            {
                using var font = new Font(name, 10F);
                _fontFamily = new FontFamily(name);
                break;
            }
            catch
            {
                // 尝试下一个字体。
            }
        }

#pragma warning disable CS0612
        AntdUI.Config.Font = new Font(FontFamily, 9F);
#pragma warning restore CS0612
    }
}
