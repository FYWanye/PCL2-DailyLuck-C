using System.Windows;

namespace RpCalculator.App;

/// <summary>
/// 简单的浅色/深色主题切换。
/// 通过替换 Application 资源中的主题字典，所有 DynamicResource 自动刷新。
/// </summary>
public static class ThemeManager
{
    public static bool IsDark { get; private set; }

    public static void Apply(bool isDark)
    {
        IsDark = isDark;

        var app = Application.Current;
        var dictionary = new ResourceDictionary
        {
            Source = new Uri(isDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };

        if (app.Resources.MergedDictionaries.Count > 0)
        {
            app.Resources.MergedDictionaries[0] = dictionary;
        }
        else
        {
            app.Resources.MergedDictionaries.Add(dictionary);
        }
    }

    public static void Toggle()
    {
        Apply(!IsDark);
    }
}
