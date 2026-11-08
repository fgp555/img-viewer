using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace IMGViewer;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp"
    };

    private string[] folderImages = [];
    private int currentIndex = -1;
    private BitmapSource? savedPhoto;
    private int rotationDegrees;
    private bool showTransparencyGrid;

    private Point? windowDragOffset;
    private Point? photoDragPosition;

    // Both the cursor and the native window position use screen pixels, including across monitors.
    private const uint MoveWithoutResizing = 0x0001 | 0x0004 | 0x0010;

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    private (ScreenRect Bounds, WindowState State, ResizeMode Resize, bool WasTopmost, WindowChrome Chrome)? windowedMode;

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreenRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public ScreenRect Monitor;
        public ScreenRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out ScreenRect bounds);

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Photo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (PhotoZoom.ScaleX > 1 && !windowDragOffset.HasValue && CaptureMouse())
            {
                photoDragPosition = e.GetPosition(PhotoViewport);
                Cursor = Cursors.Hand;
                ForceCursor = true;
            }

            e.Handled = true;
            return;
        }

        if (e.ClickCount != 2 || Photo.Source is null || windowDragOffset.HasValue)
        {
            return;
        }

        ToggleFullScreen();
        e.Handled = true;
    }

    private void ToggleFullScreen()
    {
        nint handle = new WindowInteropHelper(this).Handle;
        if (windowedMode is { } previous)
        {
            Topmost = previous.WasTopmost;
            ResizeMode = previous.Resize;
            WindowChrome.SetWindowChrome(this, previous.Chrome);
            SetScreenBounds(handle, previous.Bounds);
            WindowState = previous.State;
            windowedMode = null;
            return;
        }

        const uint nearestMonitor = 2;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, nearestMonitor), ref info))
        {
            return;
        }

        WindowState previousState = WindowState;
        WindowState = WindowState.Normal;
        if (!GetWindowRect(handle, out ScreenRect bounds))
        {
            WindowState = previousState;
            return;
        }

        windowedMode = (bounds, previousState, ResizeMode, Topmost, WindowChrome.GetWindowChrome(this));
        WindowChrome.SetWindowChrome(this, null);
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        SetScreenBounds(handle, info.Monitor);
    }

    private static void SetScreenBounds(nint handle, ScreenRect bounds)
    {
        const uint keepOrderAndActivation = 0x0004 | 0x0010;
        SetWindowPos(handle, 0, bounds.Left, bounds.Top,
            bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, keepOrderAndActivation);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (photoDragPosition.HasValue || e.ChangedButton != MouseButton.Middle || windowedMode.HasValue || WindowState != WindowState.Normal || !CaptureMouse())
        {
            return;
        }

        Point pointer = PointToScreen(e.GetPosition(this));
        Point origin = PointToScreen(new Point());
        windowDragOffset = new Point(pointer.X - origin.X, pointer.Y - origin.Y);
        Cursor = Cursors.Hand;
        ForceCursor = true;
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (photoDragPosition is Point previous)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ReleaseMouseCapture();
                return;
            }

            Point position = e.GetPosition(PhotoViewport);
            Vector movement = position - previous;
            photoDragPosition = position;
            SetPhotoPan(PhotoPan.X + movement.X, PhotoPan.Y + movement.Y);
            e.Handled = true;
            return;
        }

        if (windowDragOffset is not Point offset)
        {
            return;
        }

        if (e.MiddleButton != MouseButtonState.Pressed)
        {
            ReleaseMouseCapture();
            return;
        }

        Point pointer = PointToScreen(e.GetPosition(this));
        SetWindowPos(new WindowInteropHelper(this).Handle, 0,
            (int)Math.Round(pointer.X - offset.X), (int)Math.Round(pointer.Y - offset.Y),
            0, 0, MoveWithoutResizing);
        e.Handled = true;
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        if ((e.ChangedButton == MouseButton.Middle && windowDragOffset.HasValue) ||
            (e.ChangedButton == MouseButton.Left && photoDragPosition.HasValue))
        {
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if ((windowDragOffset.HasValue || photoDragPosition.HasValue) && !IsMouseCaptured)
        {
            windowDragOffset = null;
            photoDragPosition = null;
            ClearValue(CursorProperty);
            ClearValue(ForceCursorProperty);
        }
    }

    private void MoveButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (!windowedMode.HasValue)
        {
            DragMove();
        }
    }

    private void InfoButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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

        OpenImage(path);
    }

    internal void OpenImage(string path)
    {
        if (!IsSupportedImage(path))
        {
            StatusText.Text = "Selecciona un archivo JPG, PNG o BMP.";
            return;
        }

        try
        {
            path = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            StatusText.Text = "La ruta de la imagen no es válida.";
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
        if (e.Key == Key.Escape && windowedMode.HasValue)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        if (e.Key == Key.T && modifiers == ModifierKeys.None)
        {
            if (!e.IsRepeat)
            {
                showTransparencyGrid = !showTransparencyGrid;
                PhotoViewport.Background = (Brush)FindResource(
                    showTransparencyGrid ? "TransparencyBackground" : "ViewerBackground");
            }

            e.Handled = true;
            return;
        }

        if (currentIndex >= 0 && e.Key == Key.R && modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            if (!e.IsRepeat)
            {
                RotatePhoto(modifiers == ModifierKeys.Shift ? -90 : 90);
            }

            e.Handled = true;
            return;
        }

        if (currentIndex >= 0 && e.Key == Key.S &&
            (modifiers == ModifierKeys.Control || modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
        {
            if (!e.IsRepeat)
            {
                SavePhoto((modifiers & ModifierKeys.Shift) != 0);
            }

            e.Handled = true;
            return;
        }

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

    private void RotatePhoto(int degrees)
    {
        if (savedPhoto is null)
        {
            return;
        }

        SetRotationDegrees((rotationDegrees + degrees + 360) % 360);
        if (rotationDegrees == 0)
        {
            Photo.Source = savedPhoto;
        }
        else
        {
            var rotated = new TransformedBitmap(savedPhoto, new RotateTransform(rotationDegrees));
            rotated.Freeze();
            Photo.Source = rotated;
        }

        ResetPhotoView();
        StatusText.Text = string.Empty;
    }

    private void SetRotationDegrees(int degrees)
    {
        rotationDegrees = degrees;
        SavePendingButton.Visibility = degrees == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SavePendingButton_Click(object sender, RoutedEventArgs e)
    {
        SavePhoto(saveCopy: false);
    }

    private void SavePhoto(bool saveCopy)
    {
        if (Photo.Source is not BitmapSource bitmap)
        {
            return;
        }

        string originalPath = folderImages[currentIndex];
        string destination = originalPath;
        if (saveCopy)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Guardar copia",
                InitialDirectory = Path.GetDirectoryName(originalPath),
                FileName = Path.GetFileNameWithoutExtension(originalPath) + " - copia",
                DefaultExt = Path.GetExtension(originalPath),
                Filter = "Imágenes JPG, PNG y BMP|*.jpg;*.jpeg;*.png;*.bmp",
                AddExtension = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            destination = dialog.FileName;
            if (!IsSupportedImage(destination) || StringComparer.OrdinalIgnoreCase.Equals(destination, originalPath))
            {
                StatusText.Text = "Elige otro archivo con extensión JPG, PNG o BMP.";
                return;
            }
        }
        else if (rotationDegrees == 0)
        {
            StatusText.Text = "No hay cambios que guardar.";
            return;
        }

        try
        {
            WritePhoto(bitmap, destination, saveCopy);
            if (!saveCopy)
            {
                savedPhoto = bitmap;
                SetRotationDegrees(0);
            }

            StatusText.Text = saveCopy ? "Copia guardada." : "Cambios guardados.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            StatusText.Text = $"No se pudo guardar {Path.GetFileName(destination)}.";
        }
    }

    private static void WritePhoto(BitmapSource bitmap, string destination, bool saveCopy)
    {
        BitmapEncoder encoder = Path.GetExtension(destination).ToLowerInvariant() switch
        {
            ".png" => new PngBitmapEncoder(),
            ".bmp" => new BmpBitmapEncoder(),
            _ => new JpegBitmapEncoder { QualityLevel = 95 }
        };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        // Encode beside the destination before replacing any existing file.
        string temporaryPath = Path.Combine(Path.GetDirectoryName(destination)!, Path.GetRandomFileName());
        var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write);
        try
        {
            using (stream)
            {
                encoder.Save(stream);
            }

            if (saveCopy)
            {
                File.Move(temporaryPath, destination, overwrite: true);
            }
            else
            {
                File.Replace(temporaryPath, destination, null);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private void ResetPhotoView()
    {
        PhotoZoom.ScaleX = 1;
        PhotoZoom.ScaleY = 1;
        SetPhotoPan(0, 0);
    }

    private void PhotoViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SetPhotoPan(PhotoPan.X, PhotoPan.Y);
    }

    private void SetPhotoPan(double x, double y)
    {
        double scale = PhotoZoom.ScaleX;
        double maxX = Math.Max(0, (Photo.ActualWidth * scale - PhotoViewport.ActualWidth) / 2);
        double maxY = Math.Max(0, (Photo.ActualHeight * scale - PhotoViewport.ActualHeight) / 2);
        PhotoPan.X = Math.Clamp(x, -maxX, maxX);
        PhotoPan.Y = Math.Clamp(y, -maxY, maxY);
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (windowDragOffset.HasValue || photoDragPosition.HasValue || e.Delta == 0 || currentIndex < 0)
        {
            return;
        }

        e.Handled = true;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            double factor = Math.Pow(1.2, e.Delta / 120.0);
            double scale = Math.Clamp(PhotoZoom.ScaleX * factor, 1, 8);
            PhotoZoom.ScaleX = scale;
            PhotoZoom.ScaleY = scale;
            SetPhotoPan(PhotoPan.X, PhotoPan.Y);
            return;
        }

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

            ResetPhotoView();
            savedPhoto = bitmap;
            SetRotationDegrees(0);
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
        StatusText.Text = string.Empty;
        ImageInfo.Text = $"{Path.GetFileName(folderImages[currentIndex])}{Environment.NewLine}{currentIndex + 1} / {folderImages.Length}";
    }
}
