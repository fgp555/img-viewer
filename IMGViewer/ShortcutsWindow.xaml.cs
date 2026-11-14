using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace IMGViewer;
internal sealed record ShortcutAction(string Group, string Icon, string Action, string Label, string Shortcut)
{
    public bool Enabled { get; set; } = true;
}
public partial class ShortcutsWindow : Window
{
    private readonly List<Border> cards = [];
    private int columns;
    internal ShortcutsWindow(IEnumerable<ShortcutAction> actions, Action<string> execute)
    {
        InitializeComponent();
        foreach (var group in actions.GroupBy(action => action.Group))
        {
            var body = new StackPanel();
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8,4,8,12) };
            heading.Children.Add(new TextBlock { Text = group.First().Icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 17, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) });
            heading.Children.Add(new TextBlock { Text = group.Key, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
            body.Children.Add(heading);
            foreach (var action in group)
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1,GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
                var keys = CreateKeys(action.Shortcut); keys.Margin = new Thickness(0,0,12,0); row.Children.Add(keys);
                var label = new TextBlock { Text = action.Label, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
                Grid.SetColumn(label,1); row.Children.Add(label);
                var button = new Button { Content = row, Style = (Style)FindResource("ShortcutRow"), IsEnabled = action.Enabled, ToolTip = action.Shortcut.Length==0 ? action.Label : $"{action.Label} · {action.Shortcut}" };
                System.Windows.Automation.AutomationProperties.SetName(button, action.Label + (action.Shortcut.Length==0 ? "" : ", " + action.Shortcut));
                button.Click += (_,_) => { Close(); execute(action.Action); };
                body.Children.Add(button);
            }
            var card = new Border { Background = new SolidColorBrush(Color.FromRgb(29,31,32)), BorderBrush = new SolidColorBrush(Color.FromRgb(55,58,59)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(6), Child = body };
            cards.Add(card); Sections.Children.Add(card);
        }
        LayoutSections();
    }
    private static FrameworkElement CreateKeys(string shortcut)
    {
        if(shortcut.Length==0) return new TextBlock { Text="Sin atajo", Foreground=Brushes.Gray, FontSize=11, VerticalAlignment=VerticalAlignment.Center };
        var panel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var alternatives = shortcut.Split(" / ");
        for(int alternative=0;alternative<alternatives.Length;alternative++)
        {
            if(alternative>0) panel.Children.Add(new TextBlock { Text=" / ", Foreground=Brushes.Gray, VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(0,0,0,3) });
            var parts=alternatives[alternative].Split(" + ");
            for(int index=0;index<parts.Length;index++)
            {
                if(index>0) panel.Children.Add(new TextBlock { Text="+", Foreground=Brushes.Gray, Margin=new Thickness(3,0,3,3), VerticalAlignment=VerticalAlignment.Center });
                panel.Children.Add(new Border { Background=new SolidColorBrush(Color.FromRgb(42,45,47)), BorderBrush=new SolidColorBrush(Color.FromRgb(76,80,82)), BorderThickness=new Thickness(1,1,1,2), CornerRadius=new CornerRadius(4), Padding=new Thickness(5,3,5,3), Margin=new Thickness(0,0,0,3), Child=new TextBlock { Text=parts[index], Foreground=Brushes.White, FontSize=11, TextWrapping=TextWrapping.Wrap } });
            }
        }
        return panel;
    }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutSections();
    private void LayoutSections()
    {
        if(Sections is null || cards.Count==0) return;
        int count=ActualWidth>=860?2:1;
        if(columns==count) return;
        columns=count; Sections.ColumnDefinitions.Clear(); Sections.RowDefinitions.Clear();
        for(int col=0;col<count;col++) Sections.ColumnDefinitions.Add(new ColumnDefinition());
        for(int row=0;row<(cards.Count+count-1)/count;row++) Sections.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        for(int i=0;i<cards.Count;i++) { Grid.SetRow(cards[i],i/count); Grid.SetColumn(cards[i],i%count); }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
