using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private (string Folder, string? Image, int Columns, bool Masonry, double Offset)? lastGridPosition;
    private ScrollViewer? gridScrollViewer;
    private int loadedGridColumns;
    private GridPhoto[] gridPhotos = [];
    private int selectionAnchor = -1;

    private string? CurrentGridImage => currentIndex >= 0 ? folderImages[currentIndex] : null;

    private bool IsFolderGridOpen => FolderGrid.Visibility == Visibility.Visible;

    private void FolderGridButton_Click(object sender, RoutedEventArgs e) => ToggleFolderGrid();

    private void ToggleFolderGrid()
    {
        if (IsFolderGridOpen)
        {
            CloseFolderGrid();
            return;
        }

        viewerClickTimer.Stop();
        SetSlideShowPlaying(false);
        FolderGrid.Visibility = Visibility.Visible;
        UpdateViewerToolbar();
        ImageFooter.Visibility = Visibility.Collapsed;
        CropButton.Visibility = Visibility.Collapsed;
        InfoButton.Visibility = Visibility.Collapsed;
        UpdateSlideShowAvailability();
        LoadFolderGrid();
        if (!masonryMode)
        {
            GridRows.Focus();
        }
    }

    private void CloseFolderGrid()
    {
        if (!IsFolderGridOpen)
        {
            return;
        }

        EndGridDrag();
        if (ActiveGridScroller is { } scroller)
        {
            lastGridPosition = (GridFolderPath.Text, CurrentGridImage, loadedGridColumns, masonryMode, scroller.VerticalOffset);
        }

        FolderGrid.Visibility = Visibility.Collapsed;
        UpdateViewerToolbar();
        GridRows.ItemsSource = null;
        gridPhotos = [];
        ResetMasonry();
        selectionAnchor = -1;
        UpdateGridSelection();
        ImageFooter.Visibility = Visibility.Visible;
        CropButton.Visibility = Photo.Source is null ? Visibility.Collapsed : Visibility.Visible;
        InfoButton.Visibility = Visibility.Visible;
        UpdateSlideShowAvailability();
        Keyboard.Focus(this);
    }

    private void LoadFolderGrid(string? folderOverride = null)
    {
        string? current = CurrentGridImage;
        string folder = folderOverride ?? (current is null ? CapturePreferences.CaptureFolder : Path.GetDirectoryName(current)!);
        GridFolderPath.Text = folder;
        GridMessage.Text = string.Empty;
        SetGridStatus(string.Empty);
        GridRows.ItemsSource = null;
        gridPhotos = [];
        ResetMasonry();
        selectionAnchor = -1;
        UpdateGridSelection();
        int columns = loadedGridColumns = ViewerPreferences.GridColumns;
        UpdateGridThumbnailSize(columns);

        try
        {
            gridPhotos = Directory.EnumerateFiles(folder)
                .Where(IsSupportedImage)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(path => new GridPhoto(path, StringComparer.OrdinalIgnoreCase.Equals(path, current)))
                .ToArray();
            var photos = gridPhotos.Chunk(columns).ToArray();
            GridRows.ItemsSource = photos;
            UpdateGridLayout();

            if (photos.Length == 0)
            {
                GridMessage.Text = "No hay imágenes en esta carpeta.";
                return;
            }

            GridPhoto[]? selectedRow = photos.FirstOrDefault(row => row.Any(photo => photo.IsCurrent));
            if (lastGridPosition is { } saved && saved.Columns == columns && saved.Masonry == masonryMode &&
                StringComparer.OrdinalIgnoreCase.Equals(saved.Folder, folder) &&
                StringComparer.OrdinalIgnoreCase.Equals(saved.Image, current) && ActiveGridScroller is { } scroller)
            {
                scroller.ScrollToVerticalOffset(saved.Offset);
            }
            else if (!masonryMode)
            {
                GridRows.ScrollIntoView(selectedRow ?? photos[0]);
            }
        }
        catch (DirectoryNotFoundException)
        {
            GridMessage.Text = "La carpeta de imágenes todavía no existe.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GridMessage.Text = "No se pudo abrir la carpeta de imágenes.";
        }
    }

    private void GridRows_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        gridScrollViewer = e.OriginalSource as ScrollViewer;
    }

    private void GridRows_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsFolderGridOpen)
        {
            UpdateGridThumbnailSize(loadedGridColumns > 0 ? loadedGridColumns : ViewerPreferences.GridColumns);
        }
    }

    private void UpdateGridThumbnailSize(int columns)
    {
        double width = GridRows.ActualWidth - 4;
        GridRows.Tag = Math.Max(1, Math.Floor(width / columns));
    }

    private void GridPhoto_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GridPhoto photo })
        {
            SelectGridPhoto(photo, Keyboard.Modifiers);
        }
    }

    private void SelectGridPhoto(GridPhoto photo, ModifierKeys modifiers)
    {
        int index = Array.IndexOf(gridPhotos, photo);
        if (index < 0 || gridBatchInProgress)
        {
            return;
        }

        bool extend = modifiers.HasFlag(ModifierKeys.Shift) && selectionAnchor >= 0;
        bool add = modifiers.HasFlag(ModifierKeys.Control);
        if (!add)
        {
            foreach (GridPhoto item in gridPhotos)
            {
                item.IsSelected = false;
            }
        }

        if (extend)
        {
            int first = Math.Min(index, selectionAnchor);
            int last = Math.Max(index, selectionAnchor);
            for (int i = first; i <= last; i++)
            {
                gridPhotos[i].IsSelected = true;
            }
        }
        else
        {
            photo.IsSelected = !photo.IsSelected;
            selectionAnchor = index;
        }

        SetGridStatus(string.Empty);
        UpdateGridSelection();
    }

    private void UpdateGridSelection()
    {
        string[] paths = SelectedGridPaths();
        GridActions.Visibility = paths.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        GridSelectionInfo.Text = paths.Length switch
        {
            0 => string.Empty,
            1 => DescribePhotoFile(paths[0]),
            _ => DescribeGridSelection(paths)
        };
    }

    private static string DescribeGridSelection(string[] paths)
    {
        long bytes = 0;
        int unavailable = 0;
        foreach (string path in paths)
        {
            try
            {
                bytes += new FileInfo(path).Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                unavailable++;
            }
        }

        string count = $"{paths.Length} imágenes seleccionadas";
        return unavailable switch
        {
            0 => $"{count} · Total: {FormatFileSize(bytes)}",
            _ when unavailable == paths.Length => $"{count} · Tamaño no disponible",
            _ => $"{count} · Total parcial: {FormatFileSize(bytes)} · {unavailable} sin tamaño disponible"
        };
    }

    private void GridPhoto_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.None &&
            sender is Button { DataContext: GridPhoto photo } && !gridBatchInProgress)
        {
            OpenGridPhoto(photo);
            e.Handled = true;
        }
    }

    private void OpenGridPhoto(GridPhoto photo)
    {
        OpenImage(photo.Path);
        if (IsFolderGridOpen)
        {
            SetGridStatus(StatusText.Text);
        }
    }

    private sealed class GridPhoto(string path, bool isCurrent) : INotifyPropertyChanged
    {
        private bool selected;

        public string Path { get; } = path;
        public bool IsCurrent { get; } = isCurrent;
        public string Name => System.IO.Path.GetFileName(Path);

        public bool IsSelected
        {
            get => selected;
            set
            {
                if (selected != value)
                {
                    selected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public BitmapSource? Thumbnail
        {
            get
            {
                try
                {
                    return ReadPhotoBitmap(Path, 384);
                }
                catch (Exception ex) when (IsBatchImageError(ex))
                {
                    return null;
                }
            }
        }
    }
}
