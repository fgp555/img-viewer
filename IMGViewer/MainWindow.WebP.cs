using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Imazen.WebP;

namespace IMGViewer;

public partial class MainWindow
{
    private static bool IsWebP(string path) =>
        Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase);

    private static BitmapSource ReadWebPBitmap(string path, int maximumSide = 0)
    {
        try
        {
            byte[] encoded = File.ReadAllBytes(path);
            var info = WebPInfo.GetImageInfo(encoded);
            if (info.HasAnimation)
                throw new NotSupportedException("WebP animado todavía no está disponible.");
            byte[] pixels = WebPDecoder.Decode(encoded, out int width, out int height, WebPPixelFormat.Bgra);
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            bitmap.Freeze();
            if (maximumSide > 0 && Math.Max(width, height) > maximumSide)
            {
                double scale = (double)maximumSide / Math.Max(width, height);
                return ResizeBitmap(bitmap, Math.Max(1, (int)Math.Round(width * scale)),
                    Math.Max(1, (int)Math.Round(height * scale)));
            }

            return bitmap;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new FormatException("WebP no válido.", ex);
        }
    }

    private static void EncodeWebP(BitmapSource source, Stream stream)
    {
        var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = checked(bitmap.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        byte[] encoded = WebPEncoder.Encode(pixels, bitmap.PixelWidth, bitmap.PixelHeight,
            stride, WebPPixelFormat.Bgra, quality: ViewerPreferences.WebPLossless ? -1 : ViewerPreferences.WebPQuality);
        stream.Write(encoded);
    }
}
