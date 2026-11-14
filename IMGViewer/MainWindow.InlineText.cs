using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private TextBox? inlineText;
    private Point inlineTextOrigin;
    private double? inlineTextWidth;
    private Brush inlineTextBrush = Brushes.Red;
    private bool inlineTextContrast;
    private double inlineTextFontSize;
    private void BeginInlineText(Point origin, double? width)
    {
        EndInlineText(true);
        if (Photo.Source is not BitmapSource bitmap) return;
        inlineTextOrigin = origin; inlineTextWidth = width;
        inlineTextBrush = markerBrush; inlineTextContrast = MarkerContrast;
        inlineTextFontSize = TextSizeSlider.Value;
        double ratio = Photo.ActualWidth / bitmap.Width;
        var editor = new TextBox { AcceptsReturn = true, TextWrapping = width.HasValue ? TextWrapping.Wrap : TextWrapping.NoWrap,
            FontFamily = new FontFamily("Segoe UI"), FontSize = inlineTextFontSize * ratio,
            Foreground = inlineTextBrush, CaretBrush = Brushes.White, Background = new SolidColorBrush(Color.FromArgb(35,0,0,0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22,142,234)), BorderThickness = new Thickness(1),
            Padding = new Thickness(0), MinWidth = width.HasValue ? 0 : 36, Width = width.HasValue ? System.Math.Max(2,width.Value*ratio) : double.NaN,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        inlineText = editor;
        Canvas.SetLeft(editor, origin.X*ratio); Canvas.SetTop(editor,origin.Y*ratio);
        InlineTextCanvas.Children.Add(editor);
        editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { EndInlineText(false); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { EndInlineText(true); e.Handled = true; }
        };
        editor.LostKeyboardFocus += (_,_) => { if (ReferenceEquals(inlineText,editor)) EndInlineText(true); };
        Dispatcher.InvokeAsync(() => { if (ReferenceEquals(inlineText,editor)) editor.Focus(); });
    }
    private void EndInlineText(bool commit)
    {
        if (inlineText is not { } editor) return;
        inlineText = null;
        string value = editor.Text;
        InlineTextCanvas.Children.Remove(editor);
        if (commit && !string.IsNullOrWhiteSpace(value) && Photo.Source is BitmapSource)
        {
            var text = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), inlineTextFontSize, inlineTextBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            if (inlineTextWidth is double width) text.MaxTextWidth = System.Math.Max(1,width);
            var geometry = text.BuildGeometry(inlineTextOrigin); geometry.Freeze();
            markers.Add(new StyledMarker(geometry,inlineTextBrush,System.Math.Max(1,inlineTextFontSize/12),inlineTextContrast));
            RefreshMarkerOverlay(); UpdatePendingSaveIndicator();
        }
    }
}
