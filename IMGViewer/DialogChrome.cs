using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace IMGViewer;
public static class DialogChrome
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(DialogChrome), new PropertyMetadata(false, Changed));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Window window || args.NewValue is not true) return;
        window.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, e) => { window.Close(); e.Handled = true; }));
        window.PreviewMouseLeftButtonDown += (_, e) =>
        {
            for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            {
                if (node is Button) return;
                if (node is FrameworkElement { Name: "DialogTitleBar" }) { window.DragMove(); e.Handled = true; return; }
            }
        };
    }
}