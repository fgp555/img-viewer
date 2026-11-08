using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private const int ScreenCaptureHotkey = 1;
    private const int HotkeyMessage = 0x0312;
    private HwndSource? hotkeySource;
    private bool captureInProgress;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        nint handle = new WindowInteropHelper(this).Handle;
        const uint noRepeat = 0x4000;
        const uint printScreen = 0x2C;
        if (!RegisterHotKey(handle, ScreenCaptureHotkey, noRepeat, printScreen))
        {
            StatusText.Text = "Impr Pant no está disponible como atajo global.";
            return;
        }

        hotkeySource = HwndSource.FromHwnd(handle);
        hotkeySource.AddHook(CaptureHotkeyHook);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel)
        {
            ReleaseCaptureHotkey();
        }
    }

    private void ReleaseCaptureHotkey()
    {
        if (hotkeySource is not null)
        {
            UnregisterHotKey(hotkeySource.Handle, ScreenCaptureHotkey);
            hotkeySource.RemoveHook(CaptureHotkeyHook);
            hotkeySource = null;
        }
    }

    private nint CaptureHotkeyHook(nint window, int message, nint id, nint data, ref bool handled)
    {
        if (message == HotkeyMessage && id == ScreenCaptureHotkey)
        {
            handled = true;
            Dispatcher.InvokeAsync(CaptureScreen);
        }

        return 0;
    }

    private void CaptureScreen()
    {
        nint handle = new WindowInteropHelper(this).Handle;
        if (captureInProgress || !IsVisible || !IsWindowEnabled(handle) || IsMouseCaptured)
        {
            return;
        }

        captureInProgress = true;
        try
        {
            DesktopSnapshot snapshot = DesktopSnapshot.Capture();
            SetSlideShowPlaying(false);
            var selector = new ScreenCaptureWindow(snapshot);
            if (selector.ShowDialog() == true && selector.Result is { } bitmap)
            {
                OpenCapture(bitmap);
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }

                Activate();
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
