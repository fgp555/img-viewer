using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private (string Folder, string? Image, int Columns, double Offset)? lastGridPosition;
    private ScrollViewer? gridScrollViewer;
    private int loadedGridColumns;

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
        GridRows.Focus();
    }

    private void CloseFolderGrid()
    {
        if (!IsFolderGridOpen)
        {
            return;
        }

        if (gridScrollViewer is not null)
        {
            lastGridPosition = (GridFolderPath.Text, CurrentGridImage, loadedGridColumns, gridScrollViewer.VerticalOffset);
        }

        FolderGrid.Visibility = Visibility.Collapsed;
        UpdateViewerToolbar();
        GridRows.ItemsSource = null;
        ImageFooter.Visibility = Visibility.Visible;
        CropButton.Visibility = Photo.Source is null ? Visibility.Collapsed : Visibility.Visible;
        InfoButton.Visibility = Visibility.Visible;
        UpdateSlideShowAvailability();
        Keyboard.Focus(this);
    }

    private void LoadFolderGrid()
    {
        string? current = CurrentGridImage;
        string folder = current is null ? CapturePreferences.CaptureFolder : Path.GetDirectoryName(current)!;
        GridFolderPath.Text = folder;
        GridMessage.Text = string.Empty;
        GridRows.ItemsSource = null;
        int columns = loadedGridColumns = ViewerPreferences.GridColumns;
        UpdateGridThumbnailSize(columns);

        try
        {
            var photos = Directory.EnumerateFiles(folder)
                .Where(IsSupportedImage)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(path => new GridPhoto(path, StringComparer.OrdinalIgnoreCase.Equals(path, current)))
                .Chunk(columns)
                .ToArray();
            GridRows.ItemsSource = photos;

            if (photos.Length == 0)
            {
                GridMessage.Text = "No hay imágenes en esta carpeta.";
                return;
            }

            GridPhoto[]? selectedRow = photos.FirstOrDefault(row => row.Any(photo => photo.IsCurrent));
            if (lastGridPosition is { } saved && saved.Columns == columns &&
                StringComparer.OrdinalIgnoreCase.Equals(saved.Folder, folder) &&
                StringComparer.OrdinalIgnoreCase.Equals(saved.Image, current) && gridScrollViewer is not null)
            {
                gridScrollViewer.ScrollToVerticalOffset(saved.Offset);
            }
            else
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
        double width = GridRows.ActualWidth - SystemParameters.VerticalScrollBarWidth - 4;
        GridRows.Tag = Math.Max(1, Math.Floor(width / columns));
    }

    private void GridPhoto_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GridPhoto photo })
        {
            OpenImage(photo.Path);
            if (IsFolderGridOpen)
            {
                GridMessage.Text = StatusText.Text;
            }
        }
    }

    private sealed record GridPhoto(string Path, bool IsCurrent)
    {
        public string Name => System.IO.Path.GetFileName(Path);

        public BitmapSource? Thumbnail
        {
            get
            {
                try
                {
                    using var stream = File.OpenRead(Path);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 384;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                           NotSupportedException or FormatException or ArgumentException)
                {
                    return null;
                }
            }
        }
    }
}
