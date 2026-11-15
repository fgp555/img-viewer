using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace IMGViewer;
public partial class MainWindow
{
    private long lastMinimizeEscape;
    private bool TryExitZenWithEscape(KeyEventArgs e)
    {
        if (!zenMode || e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None ||
            Keyboard.FocusedElement is TextBoxBase or ComboBox || markerDrag.HasValue ||
            inlineCropHandles.Any(handle => handle.IsMouseCaptured) ||
            MainMenu.Items.OfType<MenuItem>().Any(item => item.IsSubmenuOpen) || Workspace.ContextMenu?.IsOpen == true) return false;
        lastMinimizeEscape = 0;
        e.Handled = true;
        if (!e.IsRepeat) SetZenMode(false);
        return true;
    }
    private void MinimizeOnDoubleEscape()
    {
        long now = Stopwatch.GetTimestamp();
        if (lastMinimizeEscape != 0 && Stopwatch.GetElapsedTime(lastMinimizeEscape, now).TotalMilliseconds <= 500)
        {
            lastMinimizeEscape = 0;
            WindowState = WindowState.Minimized;
        }
        else lastMinimizeEscape = now;
    }
}
