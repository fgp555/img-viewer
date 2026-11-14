using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private void UpdateZoomFooter()
    {
        if (ZoomFooterText is null) return;
        if (Photo.Source is not BitmapSource { PixelWidth: > 0 } bitmap || Photo.ActualWidth <= 0)
        { ZoomFooterText.Visibility = Visibility.Collapsed; return; }
        double percent = Photo.ActualWidth * PhotoZoom.ScaleX * VisualTreeHelper.GetDpi(Photo).DpiScaleX / bitmap.PixelWidth * 100;
        ZoomFooterText.Text = $"Zoom {percent:0.#}%";
        ZoomFooterText.Visibility = Visibility.Visible;
    }
}
