using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace IMGViewer;

public partial class MainWindow
{
    private DispatcherTimer? gridSelectionScroll;
    private int gridSelectionScrollVersion;

    private void StopGridSelectionScroll()
    {
        gridSelectionScrollVersion++;
        gridSelectionScroll?.Stop();
        gridSelectionScroll = null;
    }

    private void RevealSelectedGridTile(GridPhoto photo, ScrollViewer scroller, Rect tileBounds)
    {
        StopGridSelectionScroll();
        int version = gridSelectionScrollVersion;

        // Wait for the metadata footer to take its space before measuring the viewport.
        Dispatcher.InvokeAsync(() =>
        {
            if (version != gridSelectionScrollVersion || !IsFolderGridOpen || !photo.IsSelected ||
                !ReferenceEquals(ActiveGridScroller, scroller))
            {
                return;
            }

            double start = scroller.VerticalOffset;
            double end = GridSelectionOffset(tileBounds, start, scroller.ViewportHeight, scroller.ScrollableHeight);
            if (end <= start)
            {
                return;
            }

            if (!SystemParameters.ClientAreaAnimation)
            {
                scroller.ScrollToVerticalOffset(end);
                return;
            }

            var elapsed = Stopwatch.StartNew();
            var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
            gridSelectionScroll = timer;
            timer.Tick += (_, _) =>
            {
                double progress = Math.Min(1, elapsed.Elapsed.TotalMilliseconds / 180);
                double eased = 1 - Math.Pow(1 - progress, 3);
                scroller.ScrollToVerticalOffset(start + (end - start) * eased);
                if (progress >= 1)
                {
                    StopGridSelectionScroll();
                }
            };
            timer.Start();
        }, DispatcherPriority.Loaded);
    }

    private static double GridSelectionOffset(Rect tile, double offset, double viewportHeight, double scrollableHeight)
    {
        // For a tile taller than the viewport, show its top if it is still below the current offset.
        double desired = Math.Min(tile.Top, tile.Bottom - viewportHeight);
        return Math.Clamp(Math.Max(offset, desired), 0, scrollableHeight);
    }
}
