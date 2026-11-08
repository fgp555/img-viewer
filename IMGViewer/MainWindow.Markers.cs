using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private enum MarkerKind { Arrow, Rectangle }

    private readonly List<Geometry> markers = [];
    private (MarkerKind Kind, Point Start)? markerDrag;

    private bool TryStartMarker(MouseButtonEventArgs e)
    {
        MarkerKind? kind = Keyboard.Modifiers switch
        {
            ModifierKeys.Alt => MarkerKind.Arrow,
            ModifierKeys.Shift => MarkerKind.Rectangle,
            _ => null
        };

        if (kind is null || Photo.Source is not BitmapSource bitmap ||
            photoDragPosition.HasValue || windowDragOffset.HasValue ||
            Photo.ActualWidth <= 0 || Photo.ActualHeight <= 0)
        {
            return false;
        }

        if (CaptureMouse())
        {
            SetSlideShowPlaying(false);
            markerDrag = (kind.Value, GetMarkerPoint(e, bitmap));
            Cursor = Cursors.Cross;
            ForceCursor = true;
        }

        return true;
    }

    private Point GetMarkerPoint(MouseEventArgs e, BitmapSource bitmap)
    {
        Point point = e.GetPosition(Photo);
        return new Point(
            Math.Clamp(point.X / Photo.ActualWidth, 0, 1) * bitmap.Width,
            Math.Clamp(point.Y / Photo.ActualHeight, 0, 1) * bitmap.Height);
    }

    private Geometry? GetMarkerGeometry(MouseEventArgs e, BitmapSource bitmap)
    {
        if (markerDrag is not { } drag)
        {
            return null;
        }

        Point end = GetMarkerPoint(e, bitmap);
        Vector direction = end - drag.Start;
        double stroke = MarkerStroke(bitmap);
        if (direction.Length < stroke)
        {
            return null;
        }

        if (drag.Kind == MarkerKind.Rectangle)
        {
            return Math.Abs(direction.X) >= stroke && Math.Abs(direction.Y) >= stroke
                ? new RectangleGeometry(new Rect(drag.Start, end))
                : null;
        }

        double head = Math.Min(direction.Length * 0.35, stroke * 4);
        direction.Normalize();
        Vector perpendicular = new(-direction.Y, direction.X);
        Point basePoint = end - direction * head;
        var arrow = new GeometryGroup();
        arrow.Children.Add(new LineGeometry(drag.Start, end));
        arrow.Children.Add(new LineGeometry(end, basePoint + perpendicular * (head * 0.55)));
        arrow.Children.Add(new LineGeometry(end, basePoint - perpendicular * (head * 0.55)));
        return arrow;
    }

    private void UpdateMarkerPreview(MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            ReleaseMouseCapture();
            return;
        }

        if (Photo.Source is BitmapSource bitmap)
        {
            RefreshMarkerOverlay(GetMarkerGeometry(e, bitmap));
        }
    }

    private void CompleteMarker(MouseButtonEventArgs e)
    {
        if (Photo.Source is BitmapSource bitmap && GetMarkerGeometry(e, bitmap) is { } geometry)
        {
            markers.Add(geometry);
        }

        ReleaseMouseCapture();
        UpdatePendingSaveIndicator();
    }

    private void UndoMarker()
    {
        if (markers.Count > 0)
        {
            markers.RemoveAt(markers.Count - 1);
            RefreshMarkerOverlay();
            UpdatePendingSaveIndicator();
        }
    }

    private void ClearMarkers()
    {
        markers.Clear();
        MarkerOverlay.Source = null;
    }

    private void RefreshMarkerOverlay(Geometry? preview = null)
    {
        if (Photo.Source is not BitmapSource bitmap || (markers.Count == 0 && preview is null))
        {
            MarkerOverlay.Source = null;
            return;
        }

        var group = new DrawingGroup();
        using (DrawingContext drawing = group.Open())
        {
            drawing.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, bitmap.Width, bitmap.Height));
            DrawMarkers(drawing, bitmap, preview);
        }

        MarkerOverlay.Source = new DrawingImage(group);
    }

    private void DrawMarkers(DrawingContext drawing, BitmapSource bitmap, Geometry? preview)
    {
        var pen = new Pen(Brushes.Red, MarkerStroke(bitmap))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        drawing.PushClip(new RectangleGeometry(new Rect(0, 0, bitmap.Width, bitmap.Height)));
        foreach (Geometry marker in markers)
        {
            drawing.DrawGeometry(null, pen, marker);
        }

        if (preview is not null)
        {
            drawing.DrawGeometry(null, pen, preview);
        }

        drawing.Pop();
    }

    private static double MarkerStroke(BitmapSource bitmap) =>
        Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) * 0.006);

    private BitmapSource FlattenMarkers(BitmapSource bitmap)
    {
        if (markers.Count == 0)
        {
            return bitmap;
        }

        var visual = new DrawingVisual();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawImage(bitmap, new Rect(0, 0, bitmap.Width, bitmap.Height));
            DrawMarkers(drawing, bitmap, null);
        }

        var result = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight,
            bitmap.DpiX, bitmap.DpiY, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }
}
