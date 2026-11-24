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
    private int inlineTextIndex=-1;
    private Point inlineTextOrigin;
    private double? inlineTextWidth;
    private Brush inlineTextBrush = Brushes.Red;
    private bool inlineTextContrast;
    private double inlineTextFontSize;
    private void BeginInlineText(Point origin, double? width)
    {
        EndInlineText(true);
        inlineTextIndex=-1;
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
    private void EditSelectedText()
    {
        if(!HasSelectedEditableMarker||markers[selectedPastedImage].Definition is not {Kind:MarkerKind.Text} def)return;
        int index=selectedPastedImage;var mark=markers[index];
        BeginInlineText(ElementPoint(def,def.Bounds.TopLeft),def.TextWidth is {} width?width*def.ScaleX:null);
        inlineTextIndex=index;inlineTextBrush=mark.Color;inlineTextContrast=mark.Contrast;inlineTextFontSize=def.FontSize*def.ScaleY;
        if(inlineText is {} editor)
        {
            editor.Text=def.Text??"";editor.Foreground=inlineTextBrush;
            editor.FontSize=inlineTextFontSize*Photo.ActualWidth/((BitmapSource)Photo.Source).Width;
            editor.SelectAll();
        }
        RefreshMarkerOverlay();UpdatePastedHitTesting();
    }
    private void EndInlineText(bool commit)
    {
        if(inlineText is not {} editor)return;
        inlineText=null;int index=inlineTextIndex;inlineTextIndex=-1;
        string value=editor.Text;InlineTextCanvas.Children.Remove(editor);
        var before=markers.ToArray();
        if(commit && Photo.Source is BitmapSource)
        {
            if(!string.IsNullOrWhiteSpace(value))
            {
                var def=new ElementDefinition(MarkerKind.Text,new Rect(inlineTextOrigin,new Size(Math.Max(1,inlineTextWidth??1),1)),Text:value,FontSize:inlineTextFontSize,TextWidth:inlineTextWidth);
                StyledMarker mark=index>=0&&index<markers.Count?markers[index]:new StyledMarker(Geometry.Empty,inlineTextBrush,Math.Max(1,inlineTextFontSize/12),inlineTextContrast);
                mark=RebuildElement(mark with {Color=inlineTextBrush,Contrast=inlineTextContrast,Definition=def});
                if(index>=0&&index<markers.Count){markers[index]=mark;selectedPastedImage=index;RecordElementEdit(before,"text:"+Guid.NewGuid());}
                else {markers.Add(mark);selectedPastedImage=markers.Count-1;}
            }
            else if(index>=0&&index<markers.Count){markers.RemoveAt(index);selectedPastedImage=-1;RecordElementEdit(before,"text-delete:"+Guid.NewGuid());}
        }
        RefreshMarkerOverlay();UpdatePastedHitTesting();SyncSelectedElementProperties();UpdatePendingSaveIndicator();
    }
}
