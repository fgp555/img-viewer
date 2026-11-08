using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace IMGViewer;

public partial class MainWindow
{
    private bool masonryMode;
    private (GridPhoto Photo, Rect Bounds)[] masonryTiles = [];
    private readonly Dictionary<GridPhoto, ContentPresenter> masonryVisible = [];
    private GridPhoto[]? masonryLoading;
    private double[]? masonryAspects;
    private Point? gridDragStart;
    private double gridDragOffset;

    private ScrollViewer? ActiveGridScroller => masonryMode
        ? MasonryScroll
        : gridScrollViewer ?? GridRows.Template?.FindName("SquareScroll", GridRows) as ScrollViewer;

    private void GridLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        double offset = ActiveGridScroller?.VerticalOffset ?? 0;
        masonryMode = !masonryMode;
        UpdateGridLayout();
        ActiveGridScroller?.ScrollToVerticalOffset(offset);
    }

    private void UpdateGridLayout()
    {
        GridLayoutIcon.Data = (System.Windows.Media.Geometry)FindResource(masonryMode ? "SquareLayoutIcon" : "MasonryLayoutIcon");
        string label = masonryMode ? "Cambiar a cuadrado" : "Cambiar a Masonry";
        GridLayoutButton.ToolTip = label;
        AutomationProperties.SetName(GridLayoutButton, label);
        GridRows.Visibility = masonryMode ? Visibility.Collapsed : Visibility.Visible;
        MasonryScroll.Visibility = masonryMode ? Visibility.Visible : Visibility.Collapsed;
        if (masonryMode)
        {
            ReflowMasonry();
            LoadMasonryAspects();
        }
        else
        {
            ClearMasonryVisuals();
        }
    }

    private async void LoadMasonryAspects()
    {
        GridPhoto[] photos = gridPhotos;
        if (photos.Length == 0 || masonryAspects is not null || ReferenceEquals(masonryLoading, photos))
        {
            return;
        }

        masonryLoading = photos;
        double[] aspects = await Task.Run(() => photos.Select(photo =>
        {
            var dimensions = FindResizeExample([photo.Path]);
            return dimensions is { Width: > 0, Height: > 0 } size ? (double)size.Height / size.Width : 1;
        }).ToArray());

        await Dispatcher.InvokeAsync(() =>
        {
            if (!ReferenceEquals(gridPhotos, photos))
            {
                return;
            }

            masonryLoading = null;
            masonryAspects = aspects;
            if (masonryMode && IsFolderGridOpen)
            {
                ReflowMasonry();
            }
        });
    }

    private void ResetMasonry()
    {
        masonryAspects = null;
        masonryLoading = null;
        ClearMasonryVisuals();
    }

    private void ClearMasonryVisuals()
    {
        masonryTiles = [];
        masonryVisible.Clear();
        MasonryCanvas.Children.Clear();
        MasonryCanvas.Height = 0;
    }

    private void ReflowMasonry()
    {
        if (!masonryMode || loadedGridColumns <= 0 || MasonryScroll.ActualWidth <= 0)
        {
            return;
        }

        double width = MasonryScroll.ActualWidth / loadedGridColumns;
        double[] bottoms = new double[loadedGridColumns];
        var tiles = new (GridPhoto Photo, Rect Bounds)[gridPhotos.Length];
        for (int i = 0; i < gridPhotos.Length; i++)
        {
            int column = Array.IndexOf(bottoms, bottoms.Min());
            double height = Math.Max(10, width * (masonryAspects?[i] ?? 1));
            tiles[i] = (gridPhotos[i], new Rect(column * width, bottoms[column], width, height));
            bottoms[column] += height;
        }

        masonryTiles = tiles;
        MasonryCanvas.Width = MasonryScroll.ActualWidth;
        MasonryCanvas.Height = bottoms.Max();
        UpdateMasonryViewport();
    }

    private void MasonryScroll_SizeChanged(object sender, SizeChangedEventArgs e) => ReflowMasonry();

    private void MasonryScroll_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateMasonryViewport();

    private void UpdateMasonryViewport()
    {
        if (!masonryMode || MasonryScroll.ViewportHeight <= 0)
        {
            return;
        }

        const double buffer = 240;
        double start = MasonryScroll.VerticalOffset - buffer;
        double end = MasonryScroll.VerticalOffset + MasonryScroll.ViewportHeight + buffer;
        var visible = new HashSet<GridPhoto>();
        foreach (var (photo, bounds) in masonryTiles)
        {
            if (bounds.Bottom < start || bounds.Top > end)
            {
                continue;
            }

            visible.Add(photo);
            if (!masonryVisible.TryGetValue(photo, out ContentPresenter? tile))
            {
                tile = new ContentPresenter { Content = photo, ContentTemplate = (DataTemplate)FindResource("GridTile") };
                masonryVisible.Add(photo, tile);
                MasonryCanvas.Children.Add(tile);
            }

            tile.Width = bounds.Width;
            tile.Height = bounds.Height;
            Canvas.SetLeft(tile, bounds.Left);
            Canvas.SetTop(tile, bounds.Top);
        }

        foreach (GridPhoto photo in masonryVisible.Keys.Where(photo => !visible.Contains(photo)).ToArray())
        {
            MasonryCanvas.Children.Remove(masonryVisible[photo]);
            masonryVisible.Remove(photo);
        }
    }

    private void GridScrollArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.StylusDevice is null && ActiveGridScroller is { } scroller)
        {
            gridDragStart = e.GetPosition(GridScrollArea);
            gridDragOffset = scroller.VerticalOffset;
        }
    }

    private void GridScrollArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (gridDragStart is not Point start)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndGridDrag();
            return;
        }

        Vector movement = e.GetPosition(GridScrollArea) - start;
        if (!GridScrollArea.IsMouseCaptured)
        {
            if (!MovedBeyondClickThreshold(start, start + movement) || !GridScrollArea.CaptureMouse())
            {
                return;
            }

            GridScrollArea.Cursor = Cursors.SizeNS;
        }

        ActiveGridScroller?.ScrollToVerticalOffset(gridDragOffset - movement.Y);
        e.Handled = true;
    }

    private void GridScrollArea_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (GridScrollArea.IsMouseCaptured)
        {
            e.Handled = true;
        }

        EndGridDrag();
    }

    private void GridScrollArea_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource == GridScrollArea)
        {
            gridDragStart = null;
            GridScrollArea.ClearValue(CursorProperty);
        }
    }

    private void EndGridDrag()
    {
        gridDragStart = null;
        if (GridScrollArea.IsMouseCaptured)
        {
            GridScrollArea.ReleaseMouseCapture();
        }
    }
}
