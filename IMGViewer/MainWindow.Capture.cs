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
    private bool topmostHotkeyRegistered;
    private bool captureInProgress;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        RestoreStartupPlacement();
        nint handle = new WindowInteropHelper(this).Handle;
        const uint noRepeat = 0x4000;

        windowSource = HwndSource.FromHwnd(handle);
        windowSource.AddHook(WindowMessageHook);
        UpdateNativeWindowFrame();
        captureHotkey = CaptureShortcut.Load();
        captureHotkeyRegistered = RegisterHotKey(handle, ScreenCaptureHotkey, noRepeat | captureHotkey.Modifiers, captureHotkey.VirtualKey);
        UpdateCaptureShortcutLabels();
        const uint control = 0x0002;
        const uint f12 = 0x7B;
        topmostHotkeyRegistered = RegisterHotKey(handle, AlwaysOnTopHotkey, noRepeat | control, f12);
        if (!topmostHotkeyRegistered)
        {
            StatusText.Text = "Ctrl + F12 no está disponible como atajo global.";
        }
        if (!captureHotkeyRegistered)
        {
            string message = $"{captureHotkey.Label} no está disponible. Cambia el atajo en Configuración > Capturas.";
            StatusText.Text = topmostHotkeyRegistered ? message : $"{StatusText.Text} {message}";
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel)
        {
            return;
        }

        SaveStartupPlacement();
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

            if (topmostHotkeyRegistered)
            {
                UnregisterHotKey(windowSource.Handle, AlwaysOnTopHotkey);
                topmostHotkeyRegistered = false;
            }

            windowSource.RemoveHook(WindowMessageHook);
            windowSource = null;
        }
    }

    private nint WindowMessageHook(nint window, int message, nint id, nint data, ref bool handled)
    {
        if (message == 0x0024 && SetWorkspaceMaximizedBounds(window, data))
        {
            handled = true;
            return 0;
        }

        if (message == SizingMessage && TryResizeToPhoto(id, data))
        {
            handled = true;
            return 1;
        }

        if (message == HotkeyMessage && id == ScreenCaptureHotkey)
        {
            handled = true;
            Dispatcher.InvokeAsync(CaptureScreen, System.Windows.Threading.DispatcherPriority.Send);
        }
        else if (message == HotkeyMessage && id == AlwaysOnTopHotkey)
        {
            handled = true;
            ToggleForegroundTopmost(window);
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

        bool hidingUtilityWindows=floatingCaptureButton?.IsVisible==true || captureWidget?.IsVisible==true;
        captureWidget?.Close();
        captureInProgress = true;
        bool restoreViewer = IsVisible;
        WindowState previousState = WindowState;
        bool captureCompleted = false;
        try
        {
            floatingCaptureButton?.Hide();
            SetSlideShowPlaying(false);
            if (IsVisible)
            {
                Hide();
                // Give the desktop compositor time to present the windows behind the viewer.
                DwmFlush();
            }
            else if(hidingUtilityWindows)DwmFlush();
            DateTime timestamp = DateTime.Now;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            DesktopSnapshot snapshot = DesktopSnapshot.Capture();
            System.Diagnostics.Debug.WriteLine($"IMGViewer: píxeles capturados en {elapsed.ElapsedMilliseconds} ms");
            var selector = new ScreenCaptureWindow(snapshot, new Dictionary<int, IReadOnlyList<Int32Rect>>());
            selector.ContentRendered += (_, _) => System.Diagnostics.Debug.WriteLine($"IMGViewer: selector mostrado en {elapsed.ElapsedMilliseconds} ms");
            selector.Loaded += async (_, _) =>
            {
                try
                {
                    // Use original HWNDs, never hit-test the overlay. Pixels are already frozen.
                    var detected = await System.Threading.Tasks.Task.Run(snapshot.WithWindowBounds);
                    if (!selector.IsVisible) return;
                    selector.UpdateDetectedAreas(detected, new Dictionary<int, IReadOnlyList<Int32Rect>>());
                    var areas = await CaptureAreaDetector.DetectAsync(detected);
                    if (selector.IsVisible) selector.UpdateDetectedAreas(detected, areas);
                }
                catch (Exception ex) when (ex is Win32Exception or ExternalException or InvalidOperationException or ArgumentException)
                {
                    System.Diagnostics.Debug.WriteLine($"IMGViewer: detección de áreas no disponible: {ex.Message}");
                }
            };
            if (selector.ShowDialog() == true && selector.Result is { } bitmap)
            {
                captureCompleted = true;
                if (CapturePreferences.ShowThumbnail) ShowCaptureWidget(bitmap, timestamp, selector.CaptureOrigin);
                else SaveAndOpenCapture(bitmap, timestamp);
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

                if (!CapturePreferences.ShowThumbnail && Application.Current is App app)
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
            if (restoreViewer && !captureCompleted)
            {
                WindowState = previousState;
                Show();
                if (previousState != WindowState.Minimized) Activate();
            }
            else if (captureCompleted && !CapturePreferences.ShowThumbnail && Application.Current is not App)
            {
                Show();
                Activate();
            }
            captureInProgress = false;
            RefreshFloatingCaptureButton();
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    private void CaptureSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SetSlideShowPlaying(false);
        var settings = new CaptureSettingsWindow { Owner = this };
        settings.ShowDialog();
        RefreshFloatingCaptureButton();
        UpdateCaptureDestination();
        gallerySignature = null;
        RefreshGallery(true);
        if (CurrentGridImage is { } current)
        {
            folderImages = GetFolderImages(current);
            currentIndex = Array.FindIndex(folderImages, path => StringComparer.OrdinalIgnoreCase.Equals(path, current));
        }
        if (IsFolderGridOpen)
        {
            LoadFolderGrid();
        }
    }

    private void SaveAndOpenCapture(BitmapSource bitmap, DateTime timestamp)
    {
        RememberActiveCapture();
        activeCapture = null;
        captureExportPath = null;
        if (!CapturePreferences.AutoSave)
        {
            OpenCapture(bitmap);
        }
        else try
        {
            string path = SaveCaptureFile(bitmap, CapturePreferences.CaptureFolder, timestamp);
            OpenImage(path);
            if (!StringComparer.OrdinalIgnoreCase.Equals(CurrentGridImage, path))
            {
                OpenCapture(bitmap);
                StatusText.Text = "La captura se guardó, pero no se pudo volver a abrir el archivo.";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or
                                   InvalidOperationException or ArgumentException or ExternalException)
        {
            OpenCapture(bitmap);
            StatusText.Text = "No se pudo guardar la captura automáticamente. Usa Ctrl + S para elegir una ubicación.";
        }

        AddCaptureToHistory(timestamp);
        UpdateCaptureDestination();
        HideCaptureFrame();
        viewerBackgroundMode = 0;
        UpdateBackgroundToggle();
        PhotoViewport.Background = (System.Windows.Media.Brush)FindResource("ViewerBackground");
        SetZenMode(false);
        toolsOpen = true;
        ArrowFormPicker.SelectedIndex = 0;
        MarkerWidthSlider.Value = 60;
        ArrowTool.IsChecked = true;
        activeMarkerTool = MarkerKind.Arrow;
        panTool = false;
        if (Width < 560 && WindowState == WindowState.Normal) Width = 760;
        ApplyWorkspaceLayout();
        EnableInlineCrop();
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
        imageOpenVersion++;
        deferImageDetails = false;
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
