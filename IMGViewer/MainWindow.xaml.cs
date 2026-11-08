using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp"
    };

    private string[] folderImages = [];
    private int currentIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths)
        {
            return;
        }

        string? path = paths.FirstOrDefault(IsSupportedImage);
        if (path is null)
        {
            StatusText.Text = "Suelta un archivo JPG, PNG o BMP.";
            return;
        }

        if (!TryLoadPhoto(path))
        {
            return;
        }

        folderImages = GetFolderImages(path);
        currentIndex = Array.FindIndex(folderImages, file =>
            StringComparer.OrdinalIgnoreCase.Equals(file, path));
        ShowPosition();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        int direction = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            _ => 0
        };

        if (direction == 0 || currentIndex < 0)
        {
            return;
        }

        e.Handled = true;
        Navigate(direction);
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || currentIndex < 0)
        {
            return;
        }

        e.Handled = true;
        Navigate(-Math.Sign(e.Delta));
    }

    private void Navigate(int direction)
    {
        // Skip unreadable files so they do not block access to the rest of the folder.
        for (int index = currentIndex + direction; index >= 0 && index < folderImages.Length; index += direction)
        {
            if (TryLoadPhoto(folderImages[index]))
            {
                currentIndex = index;
                ShowPosition();
                return;
            }
        }
    }

    private static bool IsSupportedImage(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    private static string[] GetFolderImages(string path)
    {
        try
        {
            return Directory.EnumerateFiles(Path.GetDirectoryName(path)!)
                .Where(IsSupportedImage)
                .Append(path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [path];
        }
    }

    private bool TryLoadPhoto(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            Photo.Source = bitmap;
            EmptyState.Visibility = Visibility.Collapsed;
            Title = $"IMG Viewer — {Path.GetFileName(path)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FormatException)
        {
            StatusText.Text = $"No se pudo abrir {Path.GetFileName(path)}.";
            return false;
        }
    }

    private void ShowPosition()
    {
        StatusText.Text = $"{Path.GetFileName(folderImages[currentIndex])}  ·  {currentIndex + 1} / {folderImages.Length}";
    }
}
