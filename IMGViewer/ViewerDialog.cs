using System.Windows;
using System.Windows.Controls;
namespace IMGViewer;
internal static class ViewerDialog
{
    internal static Window Create(Window? owner, string title, double width = 460)
    {
        var resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/IMGViewer;component/DialogTheme.xaml") };
        var window = new Window { Title = title, Width = width, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner };
        window.Resources.MergedDictionaries.Add(resources);
        window.Style = (Style)resources["DialogWindow"];
        if (owner is { IsVisible: true }) window.Owner = owner;
        return window;
    }
    internal static MessageBoxResult Show(string text, string title) => Show(null, text, title);
    internal static MessageBoxResult Show(Window? owner, string text, string title, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        var window = Create(owner, title);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24,22,24,24) });
        panel.Children.Add(new Separator());
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16,12,16,12) };
        MessageBoxResult result = buttons is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel ? MessageBoxResult.No : MessageBoxResult.Cancel;
        var choices = buttons switch
        {
            MessageBoxButton.YesNo => new[] { ("Sí", MessageBoxResult.Yes), ("No", MessageBoxResult.No) },
            MessageBoxButton.YesNoCancel => new[] { ("Sí", MessageBoxResult.Yes), ("No", MessageBoxResult.No), ("Cancelar", MessageBoxResult.Cancel) },
            MessageBoxButton.OKCancel => new[] { ("Aceptar", MessageBoxResult.OK), ("Cancelar", MessageBoxResult.Cancel) },
            _ => new[] { ("Aceptar", MessageBoxResult.OK) }
        };
        foreach (var (label, choice) in choices)
        {
            var button = new Button { Content = label, MinWidth = 78, Margin = new Thickness(4,0,0,0), IsDefault = choice == defaultResult || defaultResult == MessageBoxResult.None && choice == choices[0].Item2 };
            button.Click += (_,_) => { result = choice; window.Close(); };
            actions.Children.Add(button);
        }
        panel.Children.Add(actions); window.Content = panel; window.ShowDialog(); return result;
    }
}
