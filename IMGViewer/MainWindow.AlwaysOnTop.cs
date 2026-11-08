using System.Runtime.InteropServices;

namespace IMGViewer;

public partial class MainWindow
{
    private const int AlwaysOnTopHotkey = 2;

    private void ToggleForegroundTopmost(nint viewerHandle)
    {
        nint foreground = GetForegroundWindow();
        if (foreground == 0 || foreground == GetDesktopWindow() || foreground == GetShellWindow())
        {
            return;
        }

        if (foreground == viewerHandle)
        {
            Topmost = !Topmost;
            if (windowedMode is { } previous)
            {
                previous.WasTopmost = Topmost;
                windowedMode = previous;
            }
            return;
        }

        if (!ToggleWindowTopmost(foreground))
        {
            StatusText.Text = "No se pudo cambiar Siempre visible en la ventana activa.";
        }
    }

    private static bool ToggleWindowTopmost(nint window)
    {
        const int extendedStyle = -20;
        const int topmostStyle = 0x00000008;
        const uint keepSizePositionAndFocus = 0x0001 | 0x0002 | 0x0010;
        bool isTopmost = (GetWindowLongW(window, extendedStyle) & topmostStyle) != 0;
        nint order = isTopmost ? -2 : -1;
        return SetWindowPos(window, order, 0, 0, 0, 0, keepSizePositionAndFocus);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetDesktopWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetShellWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetWindowLongW(nint window, int index);
}
