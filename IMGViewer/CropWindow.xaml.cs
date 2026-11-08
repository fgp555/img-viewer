using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class CropWindow : Window
{
    private const double HandleRadius = 5;
    private const double MinimumCropSize = 16;
    private readonly BitmapSource source;
    private readonly Rect imageBounds;
    private readonly double previewScale;
    private Rect crop;
    private Rect dragStartCrop;
    private Point dragStart;
    private Grip? activeGrip;

    private enum Grip { Move, TopLeft, TopRight, BottomLeft, BottomRight }

    internal BitmapSource? Result { get; private set; }

    internal CropWindow(BitmapSource source)
    {
        this.source = source;
        previewScale = Math.Min(740.0 / source.PixelWidth, 470.0 / source.PixelHeight);
        imageBounds = new Rect(0, 0, source.PixelWidth * previewScale, source.PixelHeight * previewScale);
        crop = imageBounds;
        InitializeComponent();

        Preview.Width = PreviewImage.Width = imageBounds.Width;
        Preview.Height = PreviewImage.Height = imageBounds.Height;
        PreviewImage.Source = source;
        UpdateFrame();
    }

    private void UpdateFrame()
    {
        Shade.Data = new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(imageBounds), new RectangleGeometry(crop));

        var lines = new GeometryGroup();
        lines.Children.Add(new RectangleGeometry(new Rect(
            crop.Left + 0.5, crop.Top + 0.5, Math.Max(0, crop.Width - 1), Math.Max(0, crop.Height - 1))));
        for (int i = 1; i < 3; i++)
        {
            double x = crop.Left + crop.Width * i / 3;
            double y = crop.Top + crop.Height * i / 3;
            lines.Children.Add(new LineGeometry(new Point(x, crop.Top), new Point(x, crop.Bottom)));
            lines.Children.Add(new LineGeometry(new Point(crop.Left, y), new Point(crop.Right, y)));
        }
        CropLines.Data = lines;

        var handles = new GeometryGroup();
        foreach (Point corner in new[] { crop.TopLeft, crop.TopRight, crop.BottomLeft, crop.BottomRight })
        {
            double x = Math.Clamp(corner.X - HandleRadius, 0, Math.Max(0, imageBounds.Width - 2 * HandleRadius));
            double y = Math.Clamp(corner.Y - HandleRadius, 0, Math.Max(0, imageBounds.Height - 2 * HandleRadius));
            handles.Children.Add(new RectangleGeometry(new Rect(x, y, 2 * HandleRadius, 2 * HandleRadius)));
        }
        Handles.Data = handles;
    }

    private Grip? GetGrip(Point point)
    {
        const double hitRadius = 12;
        if ((point - crop.TopLeft).Length <= hitRadius) return Grip.TopLeft;
        if ((point - crop.TopRight).Length <= hitRadius) return Grip.TopRight;
        if ((point - crop.BottomLeft).Length <= hitRadius) return Grip.BottomLeft;
        if ((point - crop.BottomRight).Length <= hitRadius) return Grip.BottomRight;
        return crop.Contains(point) ? Grip.Move : null;
    }

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Point point = e.GetPosition(Preview);
        if (GetGrip(point) is not Grip grip || !Preview.CaptureMouse())
        {
            return;
        }

        activeGrip = grip;
        dragStart = point;
        dragStartCrop = crop;
        e.Handled = true;
    }

    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        Point point = e.GetPosition(Preview);
        Preview.Cursor = (activeGrip ?? GetGrip(point)) switch
        {
            Grip.Move => Cursors.SizeAll,
            Grip.TopLeft or Grip.BottomRight => Cursors.SizeNWSE,
            Grip.TopRight or Grip.BottomLeft => Cursors.SizeNESW,
            _ => Cursors.Arrow
        };

        if (activeGrip is not Grip grip)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            Preview.ReleaseMouseCapture();
            return;
        }

        Vector delta = point - dragStart;
        Rect start = dragStartCrop;
        if (grip == Grip.Move)
        {
            crop.X = Math.Clamp(start.X + delta.X, 0, Math.Max(0, imageBounds.Width - start.Width));
            crop.Y = Math.Clamp(start.Y + delta.Y, 0, Math.Max(0, imageBounds.Height - start.Height));
        }
        else
        {
            double minWidth = Math.Min(MinimumCropSize, imageBounds.Width);
            double minHeight = Math.Min(MinimumCropSize, imageBounds.Height);
            double left = start.Left, top = start.Top, right = start.Right, bottom = start.Bottom;
            if (grip is Grip.TopLeft or Grip.BottomLeft)
                left = Math.Clamp(left + delta.X, 0, Math.Max(0, right - minWidth));
            else
                right = Math.Clamp(right + delta.X, Math.Min(imageBounds.Width, left + minWidth), imageBounds.Width);

            if (grip is Grip.TopLeft or Grip.TopRight)
                top = Math.Clamp(top + delta.Y, 0, Math.Max(0, bottom - minHeight));
            else
                bottom = Math.Clamp(bottom + delta.Y, Math.Min(imageBounds.Height, top + minHeight), imageBounds.Height);

            crop = new Rect(new Point(left, top), new Point(right, bottom));
        }

        UpdateFrame();
    }

    private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Preview.ReleaseMouseCapture();

    private void Preview_LostMouseCapture(object sender, MouseEventArgs e) => activeGrip = null;

    private void Angle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PreviewRotation is not null)
        {
            PreviewRotation.Angle = e.NewValue;
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => Angle.Value = 0;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.IsRepeat)
        {
            Apply();
            e.Handled = true;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => Apply();

    private void Apply()
    {
        if (Angle.Value == 0 && crop == imageBounds)
        {
            DialogResult = true;
            return;
        }

        int left = Math.Clamp((int)Math.Round(crop.Left / previewScale), 0, source.PixelWidth - 1);
        int top = Math.Clamp((int)Math.Round(crop.Top / previewScale), 0, source.PixelHeight - 1);
        int right = Math.Clamp((int)Math.Round(crop.Right / previewScale), left + 1, source.PixelWidth);
        int bottom = Math.Clamp((int)Math.Round(crop.Bottom / previewScale), top + 1, source.PixelHeight);
        if (Angle.Value == 0)
        {
            Result = new CroppedBitmap(source, new Int32Rect(left, top, right - left, bottom - top));
        }
        else
        {
            var visual = new DrawingVisual();
            using (DrawingContext drawing = visual.RenderOpen())
            {
                drawing.PushTransform(new TranslateTransform(-left, -top));
                drawing.PushTransform(new RotateTransform(Angle.Value, source.PixelWidth / 2.0, source.PixelHeight / 2.0));
                drawing.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
                drawing.Pop();
                drawing.Pop();
            }

            var bitmap = new RenderTargetBitmap(right - left, bottom - top, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            Result = bitmap;
        }

        Result.Freeze();
        DialogResult = true;
    }
}

