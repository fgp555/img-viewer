using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;

namespace IMGViewer;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp"
    };

    private string[] folderImages = [];
    private int currentIndex = -1;
    private RecycledPhoto? lastRecycledPhoto;
    private readonly DispatcherTimer recycleNoticeTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer slideShowTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private BitmapSource? lastSavedPhoto;
    private BitmapSource? rotationBasePhoto;
    private int rotationDegrees;
    private bool hasPixelEdit;
    private bool showTransparencyGrid;

    private readonly DispatcherTimer viewerClickTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime())
    };
    private bool viewerControlsVisible;
    private Point? viewerPressStart;
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

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetDoubleClickTime();

    public MainWindow()
    {
        InitializeComponent();
        viewerClickTimer.Tick += (_, _) => ToggleViewerControls();
        slideShowTimer.Tick += SlideShowTimer_Tick;
        recycleNoticeTimer.Tick += (_, _) => HideRecycleNotice();
    }

    private void Photo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TryStartMarker(e))
        {
            e.Handled = true;
            return;
        }

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

        viewerClickTimer.Stop();
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
            UpdateSlideShowAvailability();
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
        UpdateSlideShowAvailability();
    }

    protected override void OnClosed(EventArgs e)
    {
        viewerClickTimer.Stop();
        slideShowTimer.Stop();
        recycleNoticeTimer.Stop();
        base.OnClosed(e);
    }

    private void UpdateSlideShowAvailability()
    {
        bool available = !IsFolderGridOpen && windowedMode.HasValue && currentIndex >= 0 && folderImages.Length > 1;
        SlideShowButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        if (!available)
        {
            SetSlideShowPlaying(false);
        }
    }

    private void SlideShowButton_Click(object sender, RoutedEventArgs e)
    {
        SetSlideShowPlaying(!slideShowTimer.IsEnabled);
    }

    private void SetSlideShowPlaying(bool playing)
    {
        if (playing)
        {
            slideShowTimer.Start();
        }
        else
        {
            slideShowTimer.Stop();
        }

        SlideShowIcon.Data = (Geometry)FindResource(playing ? "PauseIcon" : "PlayIcon");
        string label = playing ? "Pausar presentación" : "Reproducir presentación";
        SlideShowButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(SlideShowButton, label);
    }

    private void SlideShowTimer_Tick(object? sender, EventArgs e)
    {
        int previousIndex = currentIndex;
        for (int step = 1; step < folderImages.Length; step++)
        {
            int index = (previousIndex + step) % folderImages.Length;
            if (TryShowPhotoAt(index))
            {
                return;
            }
        }

        SetSlideShowPlaying(false);
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
        viewerPressStart = null;
        if (markerDrag.HasValue || photoDragPosition.HasValue || windowDragOffset.HasValue)
        {
            return;
        }

        if (e.ChangedButton == MouseButton.Middle)
        {
            ToggleFolderGrid();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left || Keyboard.Modifiers != ModifierKeys.None ||
            !(IsFolderGridOpen ? e.OriginalSource == GridDragHeader : IsViewerSurface(e)))
        {
            return;
        }

        if (e.ClickCount > 1)
        {
            viewerClickTimer.Stop();
            return;
        }

        if (viewerClickTimer.IsEnabled)
        {
            ToggleViewerControls();
        }

        viewerPressStart = e.GetPosition(this);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (markerDrag.HasValue)
        {
            UpdateMarkerPreview(e);
            e.Handled = true;
            return;
        }

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

        if (viewerPressStart is Point start)
        {
            if (e.LeftButton != MouseButtonState.Pressed || Keyboard.Modifiers != ModifierKeys.None)
            {
                viewerPressStart = null;
                return;
            }

            if (!MovedBeyondClickThreshold(start, e.GetPosition(this)))
            {
                return;
            }

            viewerPressStart = null;
            if (windowedMode.HasValue || WindowState != WindowState.Normal || !CaptureMouse())
            {
                return;
            }

            Point origin = PointToScreen(new Point());
            Point pressed = PointToScreen(start);
            windowDragOffset = new Point(pressed.X - origin.X, pressed.Y - origin.Y);
            Cursor = Cursors.Hand;
            ForceCursor = true;
        }

        if (windowDragOffset is not Point offset)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
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
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Point? press = viewerPressStart;
        viewerPressStart = null;
        if (markerDrag.HasValue)
        {
            CompleteMarker(e);
            e.Handled = true;
            return;
        }

        if (windowDragOffset.HasValue || photoDragPosition.HasValue)
        {
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (press is Point start && !IsFolderGridOpen && Keyboard.Modifiers == ModifierKeys.None && IsViewerSurface(e) &&
            !MovedBeyondClickThreshold(start, e.GetPosition(this)))
        {
            viewerClickTimer.Start();
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        viewerPressStart = null;
        if (markerDrag.HasValue && !IsMouseCaptured)
        {
            markerDrag = null;
            ClearValue(CursorProperty);
            ClearValue(ForceCursorProperty);
            RefreshMarkerOverlay();
        }

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

    private bool IsViewerSurface(MouseEventArgs e) =>
        e.OriginalSource == Photo || e.OriginalSource == PhotoViewport || EmptyState.IsMouseOver;

    private static bool MovedBeyondClickThreshold(Point start, Point end) =>
        Math.Abs(end.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
        Math.Abs(end.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;

    private void ToggleViewerControls()
    {
        viewerClickTimer.Stop();
        viewerControlsVisible = !viewerControlsVisible;
        MetadataFooter.Visibility = viewerControlsVisible ? Visibility.Visible : Visibility.Collapsed;
        UpdateViewerToolbar();
        UpdateMetadataFooter();
    }

    private void UpdateViewerToolbar()
    {
        FloatingControls.Visibility = viewerControlsVisible || IsFolderGridOpen
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        MetadataFooter.Visibility = MetadataFooter.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        UpdateMetadataFooter();
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
        CloseFolderGrid();
        ShowPosition();
        SetSlideShowPlaying(false);
        UpdateSlideShowAvailability();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (markerDrag.HasValue)
        {
            if (e.Key == Key.Escape)
            {
                ReleaseMouseCapture();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (!e.IsRepeat)
            {
                ToggleFolderGrid();
            }

            e.Handled = true;
            return;
        }

        if (IsFolderGridOpen)
        {
            if (e.Key == Key.Escape)
            {
                if (!e.IsRepeat)
                {
                    CloseFolderGrid();
                }

                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && markers.Count > 0)
        {
            if (!e.IsRepeat)
            {
                UndoMarker();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && (HasPendingChanges || windowedMode.HasValue))
        {
            if (!e.IsRepeat)
            {
                if (HasPendingChanges)
                {
                    DiscardPendingChanges();
                }
                else
                {
                    ToggleFullScreen();
                }
            }

            e.Handled = true;
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        if (e.Key == Key.C && modifiers == ModifierKeys.Control && Photo.Source is BitmapSource bitmap)
        {
            if (!e.IsRepeat)
            {
                try
                {
                    Clipboard.SetImage(FlattenMarkers(bitmap));
                    StatusText.Text = "Imagen copiada al portapapeles.";
                }
                catch (ExternalException)
                {
                    StatusText.Text = "No se pudo copiar la imagen. Vuelve a intentarlo.";
                }
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            if (!e.IsRepeat)
            {
                if (modifiers == ModifierKeys.Shift)
                {
                    RestorePhoto();
                }
                else if (currentIndex >= 0)
                {
                    RecycleCurrentPhoto();
                }
            }

            e.Handled = true;
            return;
        }

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

        if (Photo.Source is BitmapSource && e.Key == Key.R && modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            if (!e.IsRepeat)
            {
                RotatePhoto(modifiers == ModifierKeys.Shift ? -90 : 90);
            }

            e.Handled = true;
            return;
        }

        if (Photo.Source is BitmapSource && e.Key == Key.S &&
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

    private void RecycleCurrentPhoto()
    {
        string path = folderImages[currentIndex];
        try
        {
            lastRecycledPhoto = RecycledPhoto.Recycle(path, new WindowInteropHelper(this).Handle);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or OperationCanceledException)
        {
            StatusText.Text = "No se pudo enviar la imagen a la Papelera.";
            return;
        }

        int removedIndex = currentIndex;
        folderImages = folderImages.Where((_, index) => index != removedIndex).ToArray();
        if (!NavigateFrom(removedIndex, 1) && !NavigateFrom(removedIndex - 1, -1))
        {
            currentIndex = -1;
            lastSavedPhoto = null;
            rotationBasePhoto = null;
            hasPixelEdit = false;
            CropButton.Visibility = Visibility.Collapsed;
            ClearMarkers();
            Photo.Source = null;
            ResetPhotoView();
            SetRotationDegrees(0);
            EmptyState.Visibility = Visibility.Visible;
            UpdateMetadataFooter();
            Title = "IMG Viewer";
        }

        UpdateSlideShowAvailability();
        StatusText.Text = string.Empty;
        ShowRecycleNotice();
    }

    private void ShowRecycleNotice()
    {
        recycleNoticeTimer.Stop();
        RestorePhotoButton.Visibility = Visibility.Visible;
        UpdateRecycleNoticeAnimation();
        recycleNoticeTimer.Start();
    }

    private void RestorePhotoButton_MouseEnter(object sender, MouseEventArgs e) => UpdateRecycleNoticeAnimation();

    private void RestorePhotoButton_MouseLeave(object sender, MouseEventArgs e) => UpdateRecycleNoticeAnimation();

    private void UpdateRecycleNoticeAnimation()
    {
        if (RestorePhotoButton.IsVisible && !RestorePhotoButton.IsMouseOver && SystemParameters.ClientAreaAnimation)
        {
            RestorePhotoButton.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.4, TimeSpan.FromMilliseconds(400))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            });
        }
        else
        {
            RestorePhotoButton.BeginAnimation(OpacityProperty, null);
        }
    }

    private void HideRecycleNotice()
    {
        recycleNoticeTimer.Stop();
        RestorePhotoButton.BeginAnimation(OpacityProperty, null);
        RestorePhotoButton.Visibility = Visibility.Collapsed;
    }

    private void RestorePhotoButton_Click(object sender, RoutedEventArgs e) => RestorePhoto();

    private void RestorePhoto()
    {
        if (lastRecycledPhoto is not { } photo)
        {
            StatusText.Text = "No hay una imagen que recuperar en esta sesión.";
            return;
        }

        if (Path.Exists(photo.OriginalPath))
        {
            StatusText.Text = "Ya existe un archivo con ese nombre en la ubicación original.";
            return;
        }

        try
        {
            photo.Restore(new WindowInteropHelper(this).Handle);
            lastRecycledPhoto = null;
            HideRecycleNotice();
            OpenImage(photo.OriginalPath);
            if (StatusText.Text.Length == 0)
            {
                StatusText.Text = "Imagen recuperada.";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or OperationCanceledException)
        {
            StatusText.Text = "No se pudo recuperar la imagen de la Papelera.";
        }
    }

    private void CropButton_Click(object sender, RoutedEventArgs e)
    {
        if (Photo.Source is not BitmapSource bitmap)
        {
            return;
        }

        SetSlideShowPlaying(false);
        var editor = new CropWindow(FlattenMarkers(bitmap)) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is not { } result)
        {
            return;
        }

        ClearMarkers();
        rotationBasePhoto = result;
        Photo.Source = result;
        hasPixelEdit = true;
        SetRotationDegrees(0);
        ResetPhotoView();
        StatusText.Text = string.Empty;
        UpdateMetadataFooter();
    }

    private void RotatePhoto(int degrees)
    {
        if (rotationBasePhoto is null)
        {
            return;
        }

        if (markers.Count > 0 && Photo.Source is BitmapSource bitmap)
        {
            rotationBasePhoto = FlattenMarkers(bitmap);
            ClearMarkers();
            hasPixelEdit = true;
            SetRotationDegrees(0);
        }

        SetRotationDegrees((rotationDegrees + degrees + 360) % 360);
        if (rotationDegrees == 0)
        {
            Photo.Source = rotationBasePhoto;
        }
        else
        {
            var rotated = new TransformedBitmap(rotationBasePhoto, new RotateTransform(rotationDegrees));
            rotated.Freeze();
            Photo.Source = rotated;
        }

        ResetPhotoView();
        UpdateMetadataFooter();
        StatusText.Text = string.Empty;
    }

    private void SetRotationDegrees(int degrees)
    {
        rotationDegrees = degrees;
        UpdatePendingSaveIndicator();
    }

    private bool HasPendingChanges => rotationDegrees != 0 || hasPixelEdit || markers.Count > 0;

    private void DiscardPendingChanges()
    {
        if (lastSavedPhoto is not { } bitmap)
        {
            return;
        }

        ClearMarkers();
        Photo.Source = bitmap;
        rotationBasePhoto = bitmap;
        hasPixelEdit = false;
        SetRotationDegrees(0);
        ResetPhotoView();
        UpdateMetadataFooter();
        StatusText.Text = string.Empty;
    }

    private void UpdatePendingSaveIndicator()
    {
        SavePendingButton.Visibility = HasPendingChanges || HasUnsavedCapture
            ? Visibility.Visible
            : Visibility.Collapsed;
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

        string? originalPath = currentIndex >= 0 ? folderImages[currentIndex] : null;
        string destination;
        bool chooseDestination = saveCopy || originalPath is null;
        if (chooseDestination)
        {
            var dialog = new SaveFileDialog
            {
                Title = saveCopy ? "Guardar copia" : "Guardar captura",
                InitialDirectory = originalPath is null ? string.Empty : Path.GetDirectoryName(originalPath),
                FileName = originalPath is null
                    ? CapturePreferences.FileName(DateTime.Now)
                    : Path.GetFileNameWithoutExtension(originalPath) + " - copia",
                DefaultExt = originalPath is null ? ".jpg" : Path.GetExtension(originalPath),
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
        else
        {
            if (!HasPendingChanges)
            {
                StatusText.Text = "No hay cambios que guardar.";
                return;
            }

            destination = originalPath!;
        }

        try
        {
            bitmap = FlattenMarkers(bitmap);
            WritePhoto(bitmap, destination, chooseDestination);
            if (!saveCopy)
            {
                ClearMarkers();
                Photo.Source = bitmap;
                lastSavedPhoto = bitmap;
                rotationBasePhoto = bitmap;
                hasPixelEdit = false;
                if (originalPath is null)
                {
                    folderImages = GetFolderImages(destination);
                    currentIndex = Array.FindIndex(folderImages, file =>
                        StringComparer.OrdinalIgnoreCase.Equals(file, destination));
                    Title = $"IMG Viewer — {Path.GetFileName(destination)}";
                    UpdateSlideShowAvailability();
                }

                SetRotationDegrees(0);
                UpdateMetadataFooter();
            }

            StatusText.Text = saveCopy ? "Copia guardada." : "Cambios guardados.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            StatusText.Text = $"No se pudo guardar {Path.GetFileName(destination)}.";
        }
    }

    private static void WritePhoto(BitmapSource bitmap, string destination, bool writeNewDestination, bool overwrite = true)
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

            if (writeNewDestination)
            {
                File.Move(temporaryPath, destination, overwrite);
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
        if (IsFolderGridOpen || markerDrag.HasValue || windowDragOffset.HasValue || photoDragPosition.HasValue || e.Delta == 0 || Photo.Source is null)
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

        if (currentIndex >= 0)
        {
            Navigate(-Math.Sign(e.Delta));
        }
    }

    private void Navigate(int direction)
    {
        NavigateFrom(currentIndex + direction, direction);
    }

    private bool NavigateFrom(int startIndex, int direction)
    {
        // Skip unreadable files so they do not block access to the rest of the folder.
        for (int index = startIndex; index >= 0 && index < folderImages.Length; index += direction)
        {
            if (TryShowPhotoAt(index))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryShowPhotoAt(int index)
    {
        if (!TryLoadPhoto(folderImages[index]))
        {
            return false;
        }

        currentIndex = index;
        ShowPosition();
        return true;
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
            ClearMarkers();
            lastSavedPhoto = bitmap;
            rotationBasePhoto = bitmap;
            hasPixelEdit = false;
            SetRotationDegrees(0);
            CropButton.Visibility = Visibility.Visible;
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
        UpdateMetadataFooter();
    }

    private void UpdateMetadataFooter()
    {
        if (MetadataFooter.Visibility != Visibility.Visible)
        {
            return;
        }

        if (Photo.Source is not BitmapSource bitmap)
        {
            ImageInfo.Text = "No hay una imagen abierta.";
            return;
        }

        if (HasUnsavedCapture)
        {
            ImageInfo.Text = $"Captura de pantalla · {bitmap.PixelWidth} × {bitmap.PixelHeight} px · Sin archivo";
            return;
        }

        string path = folderImages[currentIndex];
        var fields = new List<string>
        {
            Path.GetFileName(path),
            $"{currentIndex + 1} / {folderImages.Length}",
            $"{bitmap.PixelWidth} × {bitmap.PixelHeight} px"
        };

        try
        {
            using var stream = File.OpenRead(path);
            long bytes = stream.Length;
            fields.Add(bytes switch
            {
                >= 1048576 => $"{bytes / 1048576.0:0.##} MiB",
                >= 1024 => $"{bytes / 1024.0:0.##} KiB",
                _ => $"{bytes} B"
            });

            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames[0].Metadata is BitmapMetadata metadata)
            {
                string? date = metadata.DateTaken;
                string? camera = metadata.CameraModel;
                object? iso = metadata.GetQuery("/app1/ifd/exif/{ushort=34855}");

                if (!string.IsNullOrWhiteSpace(date))
                {
                    fields.Add($"Captura: {date}");
                }

                if (!string.IsNullOrWhiteSpace(camera))
                {
                    fields.Add($"Cámara: {camera}");
                }

                if (iso is ushort or uint)
                {
                    fields.Add($"ISO {iso}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   NotSupportedException or FormatException or COMException or ArgumentException)
        {
            // A missing or unreadable metadata block should not prevent viewing the image.
        }

        ImageInfo.Text = string.Join("  ·  ", fields);
    }
}
