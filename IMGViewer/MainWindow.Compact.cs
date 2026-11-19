using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace IMGViewer;
public partial class MainWindow
{
    private void ApplyCompactMode()
    {
        bool compact = ActualWidth < 640;
        MainMenu.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactMenuButton.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        DesktopActions.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactActions.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        // Avoid a narrow intermediate width wrapping the desktop toolbar into a tall column.
        if (!compact)
        {
            foreach (var button in FloatingControls.Children.OfType<Button>())
            {
                if (button == ViewModeButton || button == CropButton || button == GridLayoutButton || button == SlideShowButton) continue;
                button.Visibility = ActualWidth < 900 && (button.Tag as string) != "shortcuts" ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }
    private void ApplyCompactPanels()
    {
        bool compact = ActualWidth < 640;
        foreach (var panel in new[] { GalleryPanel, ToolsPanel })
        {
            Grid.SetColumn(panel, compact ? 2 : panel == GalleryPanel ? 0 : 4);
            Panel.SetZIndex(panel, compact ? 10 : 0);
            panel.Width = compact ? Math.Min(280, Math.Max(180, ActualWidth - 64)) : double.NaN;
            panel.HorizontalAlignment = compact ? panel == GalleryPanel ? HorizontalAlignment.Left : HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        }
        if (!compact) return;
        GalleryColumn.Width = ToolsColumn.Width = GallerySplitterColumn.Width = ToolsSplitterColumn.Width = new GridLength(0);
        GalleryColumn.MinWidth = ToolsColumn.MinWidth = 0;
        GalleryPanel.Visibility = !zenMode && !windowedMode.HasValue && galleryOpen ? Visibility.Visible : Visibility.Collapsed;
        ToolsPanel.Visibility = !zenMode && !windowedMode.HasValue && toolsOpen ? Visibility.Visible : Visibility.Collapsed;
        if (!zenMode && !windowedMode.HasValue && galleryOpen) RefreshGallery();
    }
    private void CompactMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = CompactMenuButton, Placement = PlacementMode.Bottom, MaxHeight = Math.Max(180, ActualHeight - 50) };
        foreach (MenuItem group in MainMenu.Items.OfType<MenuItem>())
        {
            menu.Items.Add(new MenuItem { Header = group.Header.ToString()?.Replace("_", ""), IsEnabled = false });
            foreach (var entry in group.Items)
            {
                if (entry is Separator) { menu.Items.Add(new Separator()); continue; }
                if (entry is not MenuItem original) continue;
                var item = new MenuItem { Header = original.Header, Tag = original.Tag, InputGestureText = original.InputGestureText, IsCheckable = original.IsCheckable };
                item.Click += WorkspaceAction_Click;
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
        }
        UpdateActionStates(menu);
        menu.IsOpen = true;
    }
}
