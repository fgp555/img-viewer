using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private const int ScreenCaptureHotkey = 1;
    private const int HotkeyMessage = 0x0312;
    private HwndSource? windowSource;
    private bool captureHotkeyRegistered;
    private bool captureInProgress;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        nint handle = new WindowInteropHelper(this).Handle;
        const uint noRepeat = 0x4000;
        const uint printScreen = 0x2C;
        windowSource = HwndSource.FromHwnd(handle);
        windowSource.AddHook(WindowMessageHook);
        captureHotkeyRegistered = RegisterHotKey(handle, ScreenCaptureHotkey, noRepeat, printScreen);
        if (!captureHotkeyRegistered)
        {
            StatusText.Text = "Impr Pant no está disponible como atajo global.";
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel)
        {
            return;
        }

        if (Application.Current is App app && app.TryHideViewer(this))
        {
            e.Cancel = true;
            SetSlideShowPlaying(false);
            return;
        }

        ReleaseWindowMessages();
    }

    private void ReleaseWindowMessages()
    {
        if (windowSource is not null)
        {
            if (captureHotkeyRegistered)
            {
                UnregisterHotKey(windowSource.Handle, ScreenCaptureHotkey);
                captureHotkeyRegistered = false;
            }

            windowSource.RemoveHook(WindowMessageHook);
            windowSource = null;
        }
    }

    private nint WindowMessageHook(nint window, int message, nint id, nint data, ref bool handled)
    {
        if (message == SizingMessage && TryResizeToPhoto(id, data))
        {
            handled = true;
            return 1;
        }

        if (message == HotkeyMessage && id == ScreenCaptureHotkey)
        {
            handled = true;
            Dispatcher.InvokeAsync(CaptureScreen);
        }

        return 0;
    }

    internal void CaptureScreen()
    {
        nint handle = new WindowInteropHelper(this).Handle;
        if (captureInProgress || handle == 0 || !IsWindowEnabled(handle) || IsMouseCaptured)
        {
            return;
        }

        captureInProgress = true;
        try
        {
            DateTime timestamp = DateTime.Now;
            DesktopSnapshot snapshot = DesktopSnapshot.Capture();
            SetSlideShowPlaying(false);
            var selector = new ScreenCaptureWindow(snapshot);
            if (selector.ShowDialog() == true && selector.Result is { } bitmap)
            {
                SaveAndOpenCapture(bitmap, timestamp);
                if (ViewerPreferences.CopyCapturesToClipboard)
                {
                    try
                    {
                        Clipboard.SetImage(bitmap);
                    }
                    catch (ExternalException)
                    {
                        string message = "No se pudo copiar la captura al portapapeles.";
                        StatusText.Text = string.IsNullOrEmpty(StatusText.Text)
                            ? message
                            : $"{StatusText.Text} {message}";
                    }
                }

                if (Application.Current is App app)
                {
                    app.ShowViewer();
                }
            }
        }
        catch (Exception ex) when (ex is Win32Exception or ExternalException or InvalidOperationException or ArgumentException)
        {
            StatusText.Text = "No se pudo capturar la pantalla.";
        }
        finally
        {
            captureInProgress = false;
        }
    }

    private void CaptureSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SetSlideShowPlaying(false);
        var settings = new CaptureSettingsWindow { Owner = this };
        settings.ShowDialog();
        if (IsFolderGridOpen)
        {
            LoadFolderGrid();
        }
    }

    private void SaveAndOpenCapture(BitmapSource bitmap, DateTime timestamp)
    {
        try
        {
            string path = SaveCaptureFile(bitmap, CapturePreferences.CaptureFolder, timestamp);
            OpenImage(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or
                                   InvalidOperationException or ArgumentException or ExternalException)
        {
            OpenCapture(bitmap);
            StatusText.Text = "No se pudo guardar la captura automáticamente. Usa Ctrl + S para elegir una ubicación.";
        }
    }

    private static string SaveCaptureFile(BitmapSource bitmap, string folder, DateTime timestamp)
    {
        Directory.CreateDirectory(folder);
        return WriteUniquePhoto(bitmap, folder, CapturePreferences.FileName(timestamp));
    }

    private static string WriteUniquePhoto(BitmapSource bitmap, string folder, string name)
    {
        string extension = Path.GetExtension(name);
        string stem = Path.GetFileNameWithoutExtension(name);
        for (int suffix = 0; ; suffix++)
        {
            string path = Path.Combine(folder, suffix == 0 ? name : $"{stem}-{suffix}{extension}");
            if (Path.Exists(path))
            {
                continue;
            }

            try
            {
                WritePhoto(bitmap, path, writeNewDestination: true, overwrite: false);
                return path;
            }
            catch (IOException) when (Path.Exists(path))
            {
                // Another instance took this name between the check and the move.
            }
        }
    }

    private void OpenCapture(BitmapSource bitmap)
    {
        folderImages = [];
        currentIndex = -1;
        ClearMarkers();
        lastSavedPhoto = bitmap;
        rotationBasePhoto = bitmap;
        Photo.Source = bitmap;
        hasPixelEdit = false;
        SetRotationDegrees(0);
        ResetPhotoView();
        CropButton.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        Title = "IMG Viewer — Captura de pantalla";
        CloseFolderGrid();
        ShowPosition();
        UpdateSlideShowAvailability();
    }

    private bool HasUnsavedCapture => currentIndex < 0 && Photo.Source is BitmapSource;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(nint window);
}
