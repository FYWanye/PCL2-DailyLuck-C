using System.Windows;

namespace RpCalculator.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Apply(isDark: false);
    }
}
