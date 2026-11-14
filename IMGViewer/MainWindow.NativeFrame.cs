using System.Runtime.InteropServices;
using System.Windows;
namespace IMGViewer;
public partial class MainWindow
{
    private (bool Bare, bool Square)? nativeFrameState;
    private void UpdateNativeWindowFrame()
    {
        if (windowSource is null || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        bool bare = zenMode || windowedMode.HasValue;
        bool square = bare || WindowState == WindowState.Maximized;
        if (nativeFrameState == (bare, square)) return;
        // An opaque HWND lets DWM own the outer silhouette, border and shadow.
        int dark = 1;
        int corners = square ? 1 : 2; // DWMWCP_DONOTROUND / DWMWCP_ROUND
        int border = bare ? unchecked((int)0xFFFFFFFE) : 0x003E3E3E; // DWMWA_COLOR_NONE / COLORREF
        DwmSetWindowAttribute(windowSource.Handle, 20, ref dark, sizeof(int));
        int result = DwmSetWindowAttribute(windowSource.Handle, 33, ref corners, sizeof(int));
        DwmSetWindowAttribute(windowSource.Handle, 34, ref border, sizeof(int));
        if (result >= 0) nativeFrameState = (bare, square);
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
