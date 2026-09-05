using System.Runtime.InteropServices;

namespace RpCalculator.App;

/// <summary>
/// 无边框窗口的原生拖动/调整大小辅助。
/// AntdUI.BorderlessForm 自带 8px 命中区；为了在阴影区域附近更好操作，
/// 这里用 WM_NCLBUTTONDOWN 直接发起系统级缩放。
/// </summary>
internal static class WindowChrome
{
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    public static void StartResize(Form form, int hitTest)
    {
        var point = Control.MousePosition;
        ReleaseCapture();
        _ = SendMessage(form.Handle, WM_NCLBUTTONDOWN, new IntPtr(hitTest), MakeLParam(point.X, point.Y));
    }

    private const uint WM_NCLBUTTONDOWN = 0x00A1;

    private static IntPtr MakeLParam(int x, int y)
    {
        return new IntPtr((y << 16) | (x & 0xFFFF));
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
