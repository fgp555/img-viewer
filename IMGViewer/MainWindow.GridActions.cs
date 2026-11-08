using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private bool gridBatchInProgress;

    private string[] SelectedGridPaths() => gridPhotos.Where(photo => photo.IsSelected).Select(photo => photo.Path).ToArray();

    private void GridCopy_Click(object sender, RoutedEventArgs e) => CopyGridFiles();

    private void CopyGridFiles()
    {
        string[] paths = SelectedGridPaths();
        if (paths.Length == 0 || gridBatchInProgress)
        {
            return;
        }

        try
        {
            var files = new StringCollection();
            files.AddRange(paths);
            Clipboard.SetFileDropList(files);
            SetGridStatus($"{paths.Length} archivo(s) copiado(s). Usa Ctrl + V en el Explorador.");
        }
        catch (Exception ex) when (ex is ExternalException or ArgumentException)
        {
            SetGridStatus("No se pudieron copiar los archivos al portapapeles.");
        }
    }

    private void GridDelete_Click(object sender, RoutedEventArgs e) => RecycleGridFiles();

    private void RecycleGridFiles()
    {
        string[] paths = SelectedGridPaths();
        if (paths.Length == 0 || gridBatchInProgress)
        {
            return;
        }

        if (paths.Length > 1 &&
            MessageBox.Show(this, $"¿Enviar {paths.Length} imágenes a la Papelera?", "IMG Viewer",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        nint owner = new WindowInteropHelper(this).Handle;
        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            try
            {
                lastRecycledPhoto = RecycledPhoto.Recycle(path, owner);
                removed.Add(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or OperationCanceledException)
            {
                // Keep the files that failed available for a retry.
            }
        }

        if (removed.Count > 0)
        {
            RefreshViewerAfterRecycling(removed);
            LoadFolderGrid(GridFolderPath.Text);
        }

        int failed = paths.Length - removed.Count;
        SetGridStatus(failed == 0
            ? $"{removed.Count} a la Papelera · Shift + Supr recupera la última."
            : $"{removed.Count} a la Papelera · {failed} no se pudieron enviar.");
    }

    private async void GridResize_Click(object sender, RoutedEventArgs e)
    {
        string[] paths = SelectedGridPaths();
        if (paths.Length == 0 || gridBatchInProgress)
        {
            return;
        }

        string folder = GridFolderPath.Text;
        var example = FindResizeExample(paths);
        if (example is null)
        {
            SetGridStatus("No se pudo leer ninguna de las imágenes seleccionadas.");
            return;
        }

        var editor = new ResizeImageWindow(example.Value.Width, example.Value.Height, paths.Length) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            return;
        }

        ImageResizeRequest request = editor.Request;
        gridBatchInProgress = true;
        GridActions.IsEnabled = false;
        GridScrollArea.IsEnabled = false;
        SetGridStatus("Redimensionando imágenes…");
        try
        {
            var (saved, unchanged, failed) = await Task.Run(() => ResizeGridFiles(paths, request));
            if (IsFolderGridOpen && StringComparer.OrdinalIgnoreCase.Equals(GridFolderPath.Text, folder))
            {
                if (saved > 0)
                {
                    if (CurrentGridImage is { } current &&
                        StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(current), folder))
                    {
                        folderImages = GetFolderImages(current);
                        currentIndex = Array.FindIndex(folderImages, path => StringComparer.OrdinalIgnoreCase.Equals(path, current));
                        UpdateMetadataFooter();
                    }

                    LoadFolderGrid(folder);
                }

                SetGridStatus($"{saved} copia(s) · {unchanged} sin cambios · {failed} con error.");
            }
        }
        finally
        {
            gridBatchInProgress = false;
            GridScrollArea.IsEnabled = true;
            GridActions.IsEnabled = true;
        }
    }

    private static (int Width, int Height)? FindResizeExample(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            try
            {
                using var stream = File.OpenRead(path);
                BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                return (frame.PixelWidth, frame.PixelHeight);
            }
            catch (Exception ex) when (IsBatchImageError(ex)) { }
        }

        return null;
    }

    private static (int Saved, int Unchanged, int Failed) ResizeGridFiles(
        IEnumerable<string> paths, ImageResizeRequest request)
    {
        int saved = 0, unchanged = 0, failed = 0;
        foreach (string path in paths)
        {
            try
            {
                BitmapSource image = ReadPhotoBitmap(path);
                var (width, height) = request.GetSize(image.PixelWidth, image.PixelHeight);
                if ((width, height) == (image.PixelWidth, image.PixelHeight))
                {
                    unchanged++;
                    continue;
                }

                if (!ImageResizeRequest.IsAllowed(width, height))
                {
                    failed++;
                    continue;
                }

                BitmapSource result = ResizeBitmap(image, width, height);
                string folder = Path.GetDirectoryName(path)!;
                string name = $"{Path.GetFileNameWithoutExtension(path)}-{width}x{height}{Path.GetExtension(path)}";
                WriteUniquePhoto(result, folder, name);
                saved++;
            }
            catch (Exception ex) when (IsBatchImageError(ex))
            {
                failed++;
            }
        }

        return (saved, unchanged, failed);
    }

    private static bool IsBatchImageError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException or FormatException or
            ArgumentException or InvalidOperationException or ExternalException or OutOfMemoryException;

    private void SetGridStatus(string message)
    {
        GridOperationStatus.Text = message;
        GridOperationStatus.Visibility = message.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HandleGridKey(KeyEventArgs e)
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Escape)
        {
            if (!e.IsRepeat)
            {
                CloseFolderGrid();
            }
        }
        else if (e.Key == Key.C && modifiers == ModifierKeys.Control)
        {
            if (!e.IsRepeat)
            {
                CopyGridFiles();
            }
        }
        else if (e.Key == Key.Delete && modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            if (!e.IsRepeat && !gridBatchInProgress)
            {
                if (modifiers == ModifierKeys.Shift)
                {
                    RestorePhoto();
                    if (IsFolderGridOpen)
                    {
                        SetGridStatus(StatusText.Text);
                    }
                }
                else
                {
                    RecycleGridFiles();
                }
            }
        }
        else if (e.Key == Key.Enter && gridPhotos.Count(photo => photo.IsSelected) == 1)
        {
            if (!e.IsRepeat && !gridBatchInProgress)
            {
                OpenGridPhoto(gridPhotos.First(photo => photo.IsSelected));
            }
        }
        else
        {
            return;
        }

        e.Handled = true;
    }
}
