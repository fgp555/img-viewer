using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class ScreenCaptureWindow : Window
{
    private readonly DesktopSnapshot snapshot;
    private Int32Rect monitor;
    private Point? dragStart;
    private bool draggingRegion;

    internal BitmapSource? Result { get; private set; }

    internal ScreenCaptureWindow(DesktopSnapshot snapshot)
    {
        this.snapshot = snapshot;
        monitor = snapshot.MonitorAt(DesktopSnapshot.CursorPosition());
        InitializeComponent();
        FrozenDesktop.Source = snapshot.Bitmap;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DesktopSnapshot.PlaceOverlay(new WindowInteropHelper(this).Handle, snapshot.Bounds);
    }

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Surface.ActualWidth <= 0 || Surface.ActualHeight <= 0 || Toolbar is null)
        {
            return;
        }

        PlaceToolbar();
        ShowSelection(WindowAt(DesktopSnapshot.CursorPosition()));
    }

    private void PlaceToolbar()
    {
        Rect target = ToView(monitor);
        Canvas.SetLeft(Toolbar, target.Left + Math.Max(0, (target.Width - Toolbar.ActualWidth) / 2));
        Canvas.SetTop(Toolbar, target.Top + 12);
    }

    private Point ToScreenPixels(Point point) => new(
        snapshot.Bounds.X + point.X * snapshot.Bounds.Width / Surface.ActualWidth,
        snapshot.Bounds.Y + point.Y * snapshot.Bounds.Height / Surface.ActualHeight);

    private Rect ToView(Int32Rect rect) => new(
        (rect.X - snapshot.Bounds.X) * Surface.ActualWidth / snapshot.Bounds.Width,
        (rect.Y - snapshot.Bounds.Y) * Surface.ActualHeight / snapshot.Bounds.Height,
        rect.Width * Surface.ActualWidth / snapshot.Bounds.Width,
        rect.Height * Surface.ActualHeight / snapshot.Bounds.Height);

    private Int32Rect WindowAt(Point point)
    {
        foreach (Int32Rect candidate in snapshot.Windows)
        {
            if (point.X >= candidate.X && point.X < candidate.X + candidate.Width &&
                point.Y >= candidate.Y && point.Y < candidate.Y + candidate.Height)
            {
                return candidate;
            }
        }

        return Int32Rect.Empty;
    }

    private void ShowSelection(Int32Rect selected)
    {
        var full = new RectangleGeometry(new Rect(0, 0, Surface.ActualWidth, Surface.ActualHeight));
        if (selected.Width <= 0 || selected.Height <= 0)
        {
            Dimmer.Data = full;
            Outline.Data = null;
            return;
        }

        var highlight = new RectangleGeometry(ToView(selected));
        Dimmer.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, highlight);
        Outline.Data = highlight;
    }

    private void Selection_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SelectionSurface.CaptureMouse())
        {
            dragStart = e.GetPosition(Surface);
            draggingRegion = false;
            e.Handled = true;
        }
    }

    private void Selection_MouseMove(object sender, MouseEventArgs e)
    {
        Point point = e.GetPosition(Surface);
        Point screenPoint = ToScreenPixels(point);
        if (dragStart is { } start)
        {
            Vector movement = point - start;
            draggingRegion |= Math.Abs(movement.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                              Math.Abs(movement.Y) >= SystemParameters.MinimumVerticalDragDistance;
            if (draggingRegion)
            {
                ShowSelection(RegionBetween(start, point));
                return;
            }
        }

        Int32Rect hoveredMonitor = snapshot.MonitorAt(screenPoint);
        if (monitor != hoveredMonitor)
        {
            monitor = hoveredMonitor;
            PlaceToolbar();
        }

        ShowSelection(WindowAt(screenPoint));
    }

    private Int32Rect RegionBetween(Point start, Point end)
    {
        Point a = ToScreenPixels(start);
        Point b = ToScreenPixels(end);
        int left = Math.Clamp((int)Math.Floor(Math.Min(a.X, b.X)), snapshot.Bounds.X, snapshot.Bounds.X + snapshot.Bounds.Width);
        int top = Math.Clamp((int)Math.Floor(Math.Min(a.Y, b.Y)), snapshot.Bounds.Y, snapshot.Bounds.Y + snapshot.Bounds.Height);
        int right = Math.Clamp((int)Math.Ceiling(Math.Max(a.X, b.X)), left, snapshot.Bounds.X + snapshot.Bounds.Width);
        int bottom = Math.Clamp((int)Math.Ceiling(Math.Max(a.Y, b.Y)), top, snapshot.Bounds.Y + snapshot.Bounds.Height);
        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private void Selection_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (dragStart is not { } start)
        {
            return;
        }

        Point point = e.GetPosition(Surface);
        Int32Rect area = draggingRegion ? RegionBetween(start, point) : WindowAt(ToScreenPixels(point));
        SelectionSurface.ReleaseMouseCapture();
        Complete(area);
        e.Handled = true;
    }

    private void Selection_LostMouseCapture(object sender, MouseEventArgs e)
    {
        dragStart = null;
        draggingRegion = false;
    }

    private void Complete(Int32Rect area)
    {
        if (area.Width > 0 && area.Height > 0)
        {
            Result = snapshot.Crop(area);
            DialogResult = true;
        }
    }

    private void CaptureMonitor_Click(object sender, RoutedEventArgs e) => Complete(monitor);

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}
