using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private void GalleryCard_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateGalleryCard((Grid)sender);
    private static void UpdateGalleryCard(Grid card)
    {
        if (card.ActualWidth <= 0) return;
        var image = card.Children.OfType<Image>().FirstOrDefault();
        double ratio = image?.Source is BitmapSource { PixelWidth: > 0, PixelHeight: > 0 } bitmap
            ? (double)bitmap.PixelHeight / bitmap.PixelWidth : 0.75;
        double height = Math.Max(1, card.ActualWidth * ratio);
        if (double.IsNaN(card.Height) || Math.Abs(card.Height - height) > 0.25) card.Height = height;
        // Border.CornerRadius alone does not clip an image child.
        card.Clip = new RectangleGeometry(new Rect(0, 0, card.ActualWidth, height), 8, 8);
    }
}
