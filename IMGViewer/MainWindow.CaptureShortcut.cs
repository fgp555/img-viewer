using System.IO;
namespace IMGViewer;
public partial class MainWindow
{
    private CaptureShortcut captureHotkey = CaptureShortcut.Default;
    internal string CaptureShortcutLabel => captureHotkey.Label;
    internal bool CaptureShortcutAvailable => captureHotkeyRegistered;
    private void UpdateCaptureShortcutLabels()
    {
        CaptureMenuItem.InputGestureText = captureHotkey.Label;
        CaptureToolbarButton.ToolTip = $"Capturar pantalla · {captureHotkey.Label}";
    }
    internal bool TrySetCaptureShortcut(CaptureShortcut shortcut, out string message)
    {
        message = "";
        if (windowSource is null) { message = "El visor aún no está listo."; return false; }
        if (shortcut == captureHotkey && captureHotkeyRegistered) return true;
        var previous = captureHotkey;
        bool wasRegistered = captureHotkeyRegistered;
        if (wasRegistered) UnregisterHotKey(windowSource.Handle, ScreenCaptureHotkey);
        captureHotkeyRegistered = RegisterHotKey(windowSource.Handle, ScreenCaptureHotkey, 0x4000 | shortcut.Modifiers, shortcut.VirtualKey);
        if (!captureHotkeyRegistered)
        {
            captureHotkeyRegistered = wasRegistered && RegisterHotKey(windowSource.Handle, ScreenCaptureHotkey, 0x4000 | previous.Modifiers, previous.VirtualKey);
            message = "El atajo está ocupado o reservado por Windows. Se conserva el anterior.";
            return false;
        }
        try { shortcut.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            UnregisterHotKey(windowSource.Handle, ScreenCaptureHotkey);
            captureHotkeyRegistered = wasRegistered && RegisterHotKey(windowSource.Handle, ScreenCaptureHotkey, 0x4000 | previous.Modifiers, previous.VirtualKey);
            message = "No se pudo guardar el atajo. Se conserva el anterior.";
            return false;
        }
        captureHotkey = shortcut;
        UpdateCaptureShortcutLabels();
        return true;
    }
}
