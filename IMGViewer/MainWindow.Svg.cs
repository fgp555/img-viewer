using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using SharpVectors.Converters;
using SharpVectors.Dom;
using SharpVectors.Renderers.Wpf;

namespace IMGViewer;

public partial class MainWindow
{
    private static bool IsSvg(string path) => Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase);

    private static BitmapSource ReadSvgBitmap(string path, int maximumSide = 0)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var xml = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            var document = XDocument.Load(xml);
            XElement element = document.Root ?? throw new FormatException("SVG vacío.");
            if (element.Name != XName.Get("svg", "http://www.w3.org/2000/svg"))
                throw new FormatException("El archivo no es SVG.");
            string[] viewBox = ((string?)element.Attribute("viewBox") ?? "")
                .Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (viewBox.Length == 4 &&
                double.TryParse(viewBox[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double viewWidth) &&
                double.TryParse(viewBox[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double viewHeight) &&
                viewWidth > 0 && viewHeight > 0)
            {
                if (element.Attribute("width") is null || ((string)element.Attribute("width")!).EndsWith('%'))
                    element.SetAttributeValue("width", viewWidth.ToString(CultureInfo.InvariantCulture));
                if (element.Attribute("height") is null || ((string)element.Attribute("height")!).EndsWith('%'))
                    element.SetAttributeValue("height", viewHeight.ToString(CultureInfo.InvariantCulture));
            }

            using var normalized = document.CreateReader();
            var settings = new WpfDrawingSettings
            {
                IncludeRuntime = false,
                ExternalResourcesAccessMode = ExternalResourcesAccessModes.Ignore
            };
            using var reader = new FileSvgReader(settings);
            DrawingGroup drawing = reader.Read(normalized) ?? throw new FormatException("SVG vacío.");
            var root = reader.SvgDocument.RootElement;
            double width = root.Width.BaseVal.Value;
            double height = root.Height.BaseVal.Value;
            if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                throw new FormatException("Dimensiones SVG no válidas.");

            double scale = maximumSide > 0 ? (double)maximumSide / Math.Max(width, height) : 1;
            scale = Math.Min(scale, Math.Min(8192 / Math.Max(width, height), Math.Sqrt(32000000 / (width * height))));
            int pixelsWide = Math.Max(1, (int)Math.Round(width * scale));
            int pixelsHigh = Math.Max(1, (int)Math.Round(height * scale));
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.PushTransform(new ScaleTransform(pixelsWide / width, pixelsHigh / height));
                context.DrawDrawing(drawing);
                context.Pop();
            }

            var bitmap = new RenderTargetBitmap(pixelsWide, pixelsHigh, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is not IOException and not UnauthorizedAccessException and not OutOfMemoryException)
        {
            throw new FormatException("No se pudo interpretar el SVG.", ex);
        }
    }
}
