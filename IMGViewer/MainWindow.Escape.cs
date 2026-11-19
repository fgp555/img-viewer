using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace IMGViewer;
public partial class MainWindow
{
    private bool TryExitZenWithEscape(KeyEventArgs e)
    {
        if (!zenMode || e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None ||
            Keyboard.FocusedElement is TextBoxBase or ComboBox || markerDrag.HasValue ||
            inlineCropHandles.Any(handle => handle.IsMouseCaptured) ||
            MainMenu.Items.OfType<MenuItem>().Any(item => item.IsSubmenuOpen) || Workspace.ContextMenu?.IsOpen == true) return false;
        e.Handled = true;
        if (!e.IsRepeat) SetZenMode(false);
        return true;
    }
}
