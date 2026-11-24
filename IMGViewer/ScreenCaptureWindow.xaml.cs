using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class ScreenCaptureWindow : Window
{
    private DesktopSnapshot snapshot;
    private Int32Rect monitor;
    private Point? dragStart;
    private bool pointerFramePending;
    private readonly System.Windows.Threading.DispatcherTimer pointerTimer=new(System.Windows.Threading.DispatcherPriority.Input)
        {Interval=TimeSpan.FromMilliseconds(1000.0/60)};
    private Point? previousPointer;
    private readonly CaptureSelectionMetrics metrics=new();
    internal event EventHandler? ManualSelectionStarted;
    private bool draggingRegion;
    private IReadOnlyDictionary<int, IReadOnlyList<Int32Rect>> regions;
    private List<Int32Rect> candidates = [];
    private int candidateIndex;
    private Int32Rect selectedArea;
    private Int32Rect? renderedSelection;
    private Size renderedSurface;
    private string? dimensionsLabel;
    private readonly TranslateTransform dimensionsPosition=new();

    internal BitmapSource? Result { get; private set; }
    internal Point CaptureOrigin { get; private set; }

    internal ScreenCaptureWindow(DesktopSnapshot snapshot, IReadOnlyDictionary<int, IReadOnlyList<Int32Rect>> regions)
    {
        this.snapshot = snapshot;
        this.regions = regions;
        monitor = snapshot.MonitorAt(DesktopSnapshot.CursorPosition());
        InitializeComponent();
        pointerTimer.Tick+=RenderPointerFrame;
        SelectionDimensions.RenderTransform=dimensionsPosition;
        FrozenDesktop.Source = snapshot.Bitmap;
        Loaded += (_, _) =>
        {
            pointerFramePending=true;pointerTimer.Start();
            Activate();
            SelectionSurface.Focus();
            Keyboard.Focus(SelectionSurface);
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        pointerTimer.Stop();pointerTimer.Tick-=RenderPointerFrame;
        metrics.Finish(snapshot.Bitmap.PixelWidth,snapshot.Bitmap.PixelHeight,RenderCapability.Tier>>16);
        base.OnClosed(e);
    }

    internal void UpdateDetectedAreas(DesktopSnapshot detectedSnapshot, IReadOnlyDictionary<int, IReadOnlyList<Int32Rect>> detectedRegions)
    {
        if (!IsVisible) return;
        snapshot = detectedSnapshot;
        regions = detectedRegions;
        if (dragStart is null && Surface.ActualWidth > 0 && Surface.ActualHeight > 0)
            UpdateCandidates(DesktopSnapshot.CursorPosition());
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
        UpdateCandidates(DesktopSnapshot.CursorPosition());
    }

    private void PlaceToolbar()
    {
        Rect target = ToView(monitor);
        Canvas.SetLeft(Toolbar, target.Left + Math.Max(0, (target.Width - Toolbar.ActualWidth) / 2));
        Canvas.SetTop(Toolbar, target.Top + 12);
        RefreshMonitorPreview();
    }

    private Point ToScreenPixels(Point point) => new(
        snapshot.Bounds.X + point.X * snapshot.Bounds.Width / Surface.ActualWidth,
        snapshot.Bounds.Y + point.Y * snapshot.Bounds.Height / Surface.ActualHeight);

    private Rect ToView(Int32Rect rect) => new(
        (rect.X - snapshot.Bounds.X) * Surface.ActualWidth / snapshot.Bounds.Width,
        (rect.Y - snapshot.Bounds.Y) * Surface.ActualHeight / snapshot.Bounds.Height,
        rect.Width * Surface.ActualWidth / snapshot.Bounds.Width,
        rect.Height * Surface.ActualHeight / snapshot.Bounds.Height);

    private bool IsMonitorPreviewActive => Toolbar.IsMouseOver || Toolbar.IsKeyboardFocusWithin;

    private void MonitorPreview_Changed(object sender, RoutedEventArgs e) => RefreshMonitorPreview();

    private void RefreshMonitorPreview()
    {
        if(dragStart is not null)return;
        bool active = IsMonitorPreviewActive;
        string description = $"Capturar este monitor · {monitor.Width} × {monitor.Height}";
        MonitorPreviewLabel.Text = active ? description : "Pantalla completa";
        Toolbar.ToolTip = description + " · Esc cancela";
        System.Windows.Automation.AutomationProperties.SetName(Toolbar, description);
        if (active)
        {
            SelectionDrawing.SetPointer(null);
            ShowSelection(monitor);
        }
        else ShowSelection(selectedArea);
    }

    private void UpdateCandidates(Point point)
    {
        // Neither pointer movement over the button nor late region detection may
        // replace the selection that will be restored after the monitor preview.
        if (dragStart is not null) return;
        if (IsMonitorPreviewActive) { RefreshMonitorPreview(); return; }
        var next = new List<Int32Rect>();
        for (int i = 0; i < snapshot.Windows.Count; i++)
        {
            Int32Rect window = snapshot.Windows[i];
            if (!Contains(window, point)) continue;
            if (regions.TryGetValue(i, out var areas))
                next.AddRange(areas.Where(area => Contains(area, point)));
            next.Add(window);
            break; // Only the frontmost window under the pointer.
        }
        next = next.Distinct().OrderBy(area => (long)area.Width * area.Height).ToList();
        if (!next.SequenceEqual(candidates)) candidateIndex = 0;
        candidates = next;
        selectedArea = candidates.Count == 0 ? Int32Rect.Empty : candidates[candidateIndex];
        ShowSelection(selectedArea);
    }

    private static bool Contains(Int32Rect area, Point point) =>
        point.X >= area.X && point.X < area.X + area.Width &&
        point.Y >= area.Y && point.Y < area.Y + area.Height;

    private void Selection_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (dragStart is not null || candidates.Count == 0) return;
        candidateIndex = (candidateIndex + (e.Delta < 0 ? 1 : -1) + candidates.Count) % candidates.Count;
        selectedArea = candidates[candidateIndex];
        ShowSelection(selectedArea);
        e.Handled = true;
    }

    private void Selection_MouseLeave(object sender, MouseEventArgs e)
    {
        if (dragStart is not null) return;
        SelectionDrawing.SetPointer(null);
    }

    private void UpdateGuides(Point point)=>SelectionDrawing.SetPointer(point);

    private void ShowSelection(Int32Rect selected)
    {
        if(dragStart is not null)SelectionDimensions.Visibility=Visibility.Collapsed;
        var surfaceSize=new Size(Surface.ActualWidth,Surface.ActualHeight);
        if(renderedSelection==selected && renderedSurface==surfaceSize)return;
        renderedSelection=selected;renderedSurface=surfaceSize;
        if(selected.Width<=0||selected.Height<=0)
        {
            SelectionDrawing.SetSelection(Rect.Empty);
            SelectionDimensions.Visibility=Visibility.Collapsed;
            return;
        }
        SelectionDrawing.SetSelection(ToView(selected));
        if(dragStart is null)ShowSelectionDimensions(selected);
    }

    private void ShowSelectionDimensions(Int32Rect selected)
    {
        if(dragStart is not null) { SelectionDimensions.Visibility=Visibility.Collapsed; return; }
        string label=$"{selected.Width} × {selected.Height} px";
        SelectionDimensions.Visibility=Visibility.Visible;
        if(dimensionsLabel!=label)
        {
            dimensionsLabel=label;SelectionDimensionsText.Text=label;

        }
        Size size=new(160,28);
        Rect selection=ToView(selected);
        // Use the monitor containing the selection corner, including negative desktop coordinates.
        Rect screen=ToView(snapshot.MonitorAt(new Point(selected.X+selected.Width-1,selected.Y+selected.Height-1)));
        screen.Intersect(new Rect(0,0,Surface.ActualWidth,Surface.ActualHeight));
        const double gap=8;
        double x=selection.Right-size.Width,y=selection.Bottom+gap;
        if(y+size.Height>screen.Bottom-gap)y=selection.Bottom-size.Height-gap;
        x=Math.Clamp(x,screen.Left+gap,Math.Max(screen.Left+gap,screen.Right-size.Width-gap));
        y=Math.Clamp(y,screen.Top+gap,Math.Max(screen.Top+gap,screen.Bottom-size.Height-gap));
        dimensionsPosition.X=x;dimensionsPosition.Y=y;
    }
    private void Selection_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Keyboard.Focus(SelectionSurface);
        if (SelectionSurface.CaptureMouse())
        {
            dragStart = e.GetPosition(Surface);
            SelectionDimensions.Visibility=Visibility.Collapsed;
            ManualSelectionStarted?.Invoke(this,EventArgs.Empty);
            metrics.StartDrag();
            pointerFramePending=true;pointerTimer.Start();
            draggingRegion = false;
            e.Handled = true;
        }
    }

    private void Selection_MouseMove(object sender, MouseEventArgs e)
    {
        // Mouse messages can arrive faster than the display; never render their backlog.
        pointerFramePending=true;
        if(!pointerTimer.IsEnabled)pointerTimer.Start();
    }

    private Point CurrentPointerView()
    {
        Point pixel=DesktopSnapshot.CursorPosition();
        return new Point((pixel.X-snapshot.Bounds.X)*Surface.ActualWidth/snapshot.Bounds.Width,
            (pixel.Y-snapshot.Bounds.Y)*Surface.ActualHeight/snapshot.Bounds.Height);
    }

    private void RenderPointerFrame(object? sender,EventArgs e)
    {
        if(!IsVisible || !pointerFramePending || Surface.ActualWidth<=0 || Surface.ActualHeight<=0)
        {pointerTimer.Stop();return;}
        pointerFramePending=false;
        Point point=CurrentPointerView();
        if(previousPointer==point){pointerTimer.Stop();return;}
        previousPointer=point;
        metrics.Sample(dragStart is not null);
        if(!SelectionSurface.IsMouseOver && dragStart is null)return;
        UpdateGuides(point);
        if(dragStart is {} start)
        {
            Vector movement=point-start;
            draggingRegion|=Math.Abs(movement.X)>=SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(movement.Y)>=SystemParameters.MinimumVerticalDragDistance;
            if(draggingRegion)
            {
                Toolbar.Visibility=Visibility.Hidden;
                ShowSelection(RegionBetween(start,point));
            }
            return;
        }
        Point screenPoint=ToScreenPixels(point);
        var hoveredMonitor=snapshot.MonitorAt(screenPoint);
        if(monitor!=hoveredMonitor){monitor=hoveredMonitor;PlaceToolbar();}
        UpdateCandidates(screenPoint);
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

        Point point=CurrentPointerView();
        Vector movement=point-start;
        bool custom=draggingRegion || Math.Abs(movement.X)>=SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(movement.Y)>=SystemParameters.MinimumVerticalDragDistance;
        Int32Rect area=custom?RegionBetween(start,point):selectedArea;
        dragStart=null;draggingRegion=false;pointerFramePending=false;
        SelectionSurface.ReleaseMouseCapture();
        Complete(area);
        e.Handled = true;
    }

    private void Selection_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if(!ReferenceEquals(e.OriginalSource,SelectionSurface)||SelectionSurface.IsMouseCaptured)return;
        dragStart = null;
        draggingRegion = false;
        Toolbar.Visibility = Visibility.Visible;
    }

    private void Complete(Int32Rect area)
    {
        if (area.Width > 0 && area.Height > 0)
        {
            CaptureOrigin = new Point(area.X + area.Width / 2.0, area.Y + area.Height / 2.0);
            Result = snapshot.Crop(area);
            DialogResult = true;
        }
    }

    private void CaptureMonitor_Click(object sender, RoutedEventArgs e)
    {
        // Keep the displayed rectangle and the captured physical monitor identical.
        ShowSelection(monitor);
        Complete(monitor);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (dragStart is not null)
            {
                dragStart = null;
                draggingRegion = false;
                SelectionSurface.ReleaseMouseCapture();
                Toolbar.Visibility = Visibility.Visible;
                selectedArea = Int32Rect.Empty;
                ShowSelection(selectedArea);
                Keyboard.Focus(SelectionSurface);
                return;
            }
            DialogResult = false;
        }
    }
}
