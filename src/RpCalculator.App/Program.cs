namespace RpCalculator.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppleTheme.ApplyGlobal();
        Application.Run(new MainForm());
    }
}
