using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace IMGViewer;
public partial class CaptureSettingsWindow
{
    private void ApplyCaptureShortcut(CaptureShortcut shortcut)
    {
        if (Owner is not MainWindow viewer) return;
        bool changed = viewer.TrySetCaptureShortcut(shortcut, out string message);
        CaptureShortcutLabel.Text = viewer.CaptureShortcutLabel;
        CaptureShortcutStatus.Text = changed ? "Atajo guardado. Disponible también desde otras aplicaciones." : message;
    }
    private void UsePrintScreen_Click(object sender, RoutedEventArgs e) => ApplyCaptureShortcut(new CaptureShortcut(0, 0x2C));
    private void ChangeCaptureShortcut_Click(object sender, RoutedEventArgs e)
    {
        var dialog = ViewerDialog.Create(this, "Cambiar atajo de captura", 440);
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Presiona una combinación con Ctrl, Alt o Shift. También puedes usar Impr Pant sola. Esc cancela.", TextWrapping = TextWrapping.Wrap });
        var preview = new TextBlock { Text = "Esperando atajo…", Margin = new Thickness(0,16,0,16), FontWeight = FontWeights.SemiBold };
        panel.Children.Add(preview);
        CaptureShortcut? selected = null;
        var save = new Button { Content = "Guardar atajo", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_,_) => { if (selected is { } shortcut) { dialog.Close(); ApplyCaptureShortcut(shortcut); } };
        panel.Children.Add(save);
        var cancel = new Button { Content = "Cancelar", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,8,0,0) };
        cancel.Click += (_,_) => dialog.Close(); panel.Children.Add(cancel);
        dialog.PreviewKeyDown += (_,args) =>
        {
            Key key = args.Key == Key.System ? args.SystemKey : args.Key;
            if (key == Key.Escape) { args.Handled = true; dialog.Close(); return; }
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
            if (key == Key.Tab || (key is Key.Enter or Key.Space && Keyboard.FocusedElement is Button { IsEnabled: true })) return;
            args.Handled = true;
            var mods = Keyboard.Modifiers;
            if ((mods & ModifierKeys.Windows) != 0 || (mods == ModifierKeys.None && key != Key.Snapshot))
            { preview.Text = "Usa Ctrl, Alt o Shift con otra tecla."; selected = null; save.IsEnabled = false; return; }
            uint nativeMods = ((mods & ModifierKeys.Control) != 0 ? 2u : 0) | ((mods & ModifierKeys.Alt) != 0 ? 1u : 0) | ((mods & ModifierKeys.Shift) != 0 ? 4u : 0);
            selected = new CaptureShortcut(nativeMods, (uint)KeyInterop.VirtualKeyFromKey(key));
            preview.Text = selected.Value.Label; save.IsEnabled = true;
        };
        dialog.Content = panel;
        dialog.ShowDialog();
    }
}
