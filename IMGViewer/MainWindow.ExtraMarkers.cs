using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private void ActivateExtraMarker(MarkerKind kind)
    {
        if (Photo.Source is null || IsFolderGridOpen) return;
        SetZenMode(false); toolsOpen = true; panTool = false;
        if (kind == MarkerKind.Text) TextTool.IsChecked = true;
        else if (kind == MarkerKind.Blur) BlurTool.IsChecked = true;
        else FreehandTool.IsChecked = true;
        activeMarkerTool = kind; ApplyWorkspaceLayout();
    }
    private static BitmapSource CreateBlurPatch(BitmapSource source, Rect bounds)
    {
        // Render at source DPI so the exported blur matches the preview.
        var image = new Image { Source = source, Width = source.Width, Height = source.Height,
            Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 18, KernelType = System.Windows.Media.Effects.KernelType.Gaussian, RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality } };
        image.Measure(new Size(source.Width,source.Height)); image.Arrange(new Rect(0,0,source.Width,source.Height)); image.UpdateLayout();
        var visual = new DrawingVisual();
        using(var drawing=visual.RenderOpen())
        {
            drawing.DrawRectangle(new VisualBrush(image) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds }, null, new Rect(0,0,bounds.Width,bounds.Height));
        }
        int width=Math.Max(1,(int)System.Math.Ceiling(bounds.Width*source.DpiX/96));
        int height=Math.Max(1,(int)System.Math.Ceiling(bounds.Height*source.DpiY/96));
        var result=new RenderTargetBitmap(width,height,source.DpiX,source.DpiY,PixelFormats.Pbgra32); result.Render(visual); result.Freeze(); return result;
    }
}
