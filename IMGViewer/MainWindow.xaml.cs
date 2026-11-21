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
        ".jpg", ".jpeg", ".png", ".bmp", ".svg", ".webp"
    };

    private string[] folderImages = [];
    private int currentIndex = -1;
    private readonly Stack<RecycledPhoto> recycledPhotos = new();
    private readonly DispatcherTimer recycleNoticeTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer slideShowTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private BitmapSource? lastSavedPhoto;
    private BitmapSource? rotationBasePhoto;
    private int rotationDegrees;
    private bool hasPixelEdit;
    private int viewerBackgroundMode;
    private bool coverPhoto;
    private double photoZoomMultiplier = 1;

    private readonly DispatcherTimer viewerClickTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime())
    };
    private Point? viewerPressStart;
    private Point? windowDragOffset;
    private Point? photoDragPosition;
    private bool middlePhotoDrag;
    private Point? middlePressStart;
    private bool middlePressMoved;

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
        MarkerWidthSlider.ValueChanged+=MarkerWidthChanged;
        SyncMarkerWidthControl();

        UpdateMetadataFooter();
        InitializeWorkspace();
        InitializeCloud();
        draftSaveTimer.Tick+=(_,_)=>{draftSaveTimer.Stop();RememberActiveCapture();};
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

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 || panTool)
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
            ApplyWorkspaceLayout();
            UpdateNativeWindowFrame();
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
        ApplyWorkspaceLayout();
        UpdateNativeWindowFrame();
        UpdateSlideShowAvailability();
    }

    protected override void OnClosed(EventArgs e)
    {
        dimensionsCopyFeedback?.Stop();
        CloseFloatingCaptureButton();
        FlushCaptureDrafts();
        imageOpenVersion++;
        CancelInlineCrop();
        viewerClickTimer.Stop();
        slideShowTimer.Stop();
        recycleNoticeTimer.Stop();
        CancelFolderGridLoad();
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

        SlideShowIcon.Text = playing ? "\uE769" : "\uE768";
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
        if(cropCommitTimer is not null && IsViewerSurface(e)) {e.Handled=true;return;}
        if(e.ChangedButton==MouseButton.Left && IsPastedImageThumb(e.OriginalSource as DependencyObject))return;
        if (CurveHandle.IsMouseOver || (InlineCropCanvas.IsVisible && GalleryAncestor<System.Windows.Controls.Primitives.Thumb>(e.OriginalSource as DependencyObject) is not null)) return;
        if (e.ChangedButton == MouseButton.Left && !IsFolderGridOpen && IsViewerSurface(e)) Keyboard.Focus(this);
        viewerPressStart = null;
        if (markerDrag.HasValue || photoDragPosition.HasValue || windowDragOffset.HasValue)
        {
            return;
        }

        if (e.ChangedButton == MouseButton.Middle)
        {
            if (!IsFolderGridOpen && Photo.Source is not null && PhotoViewport.IsMouseOver && CaptureMouse())
            {
                middlePhotoDrag = true;
                middlePressStart = e.GetPosition(PhotoViewport);
                middlePressMoved = false;
                photoDragPosition = middlePressStart;
                Cursor = Cursors.Hand;
                ForceCursor = true;
            }
            e.Handled = true;
            return;
        }

        if (!zenMode && (activeMarkerTool is not null || panTool) && !IsFolderGridOpen && IsViewerSurface(e)) return;

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
            if ((middlePhotoDrag ? e.MiddleButton : e.LeftButton) != MouseButtonState.Pressed)
            {
                ReleaseMouseCapture();
                return;
            }

            Point position = e.GetPosition(PhotoViewport);
            if (middlePhotoDrag && !middlePressMoved && middlePressStart is Point middleStart)
            {
                if (!MovedBeyondClickThreshold(middleStart, position)) { e.Handled = true; return; }
                middlePressMoved = true;
            }
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
        if (middlePhotoDrag && e.ChangedButton == MouseButton.Middle)
        {
            bool toggleFit = !middlePressMoved && middlePressStart is Point middleOrigin &&
                !MovedBeyondClickThreshold(middleOrigin, e.GetPosition(PhotoViewport));
            Point anchor = middlePressStart ?? e.GetPosition(PhotoViewport);
            Point imageAnchor = PhotoViewport.TranslatePoint(anchor, Photo);
            ReleaseMouseCapture();
            if (toggleFit && Photo.Source is not null)
            {
                if (Math.Abs(photoZoomMultiplier - 1) > 0.0001)
                {
                    coverPhoto = false;
                    ResetPhotoView();
                }
                else
                {
                    coverPhoto = !coverPhoto;
                    UpdatePhotoZoom();
                    Point movedAnchor = Photo.TranslatePoint(imageAnchor, PhotoViewport);
                    SetPhotoPan(PhotoPan.X + anchor.X - movedAnchor.X,
                        PhotoPan.Y + anchor.Y - movedAnchor.Y);
                }
            }
            e.Handled = true;
            return;
        }
        if (middlePhotoDrag) return;
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
            // A simple click leaves workspace controls unchanged.
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
            middlePhotoDrag = false;
            middlePressStart = null;
            middlePressMoved = false;
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

    private void UpdateViewerToolbar()
    {
        bool inGrid = IsFolderGridOpen;
        if (GridFooter is not null) GridFooter.Visibility = !zenMode && inGrid ? Visibility.Visible : Visibility.Collapsed;
        ViewModeIcon.Text = inGrid ? "\uE8B9" : "\uF0E2";
        UpdateToolbarActions();
        string label = inGrid ? "Volver a imagen" : "Abrir cuadrícula";
        ViewModeButton.ToolTip = $"{label} · G";
        System.Windows.Automation.AutomationProperties.SetName(ViewModeButton, label);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && Application.Current is App app)
        {
            app.ExitViewer();
            return;
        }

        Close();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !galleryExternalDrag && e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (galleryExternalDrag) { e.Handled = true; return; }
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths)
        {
            return;
        }

        string? path = paths.FirstOrDefault(IsSupportedImage);
        if (path is null)
        {
            StatusText.Text = "Suelta un archivo JPG, PNG, BMP, SVG o WebP.";
            return;
        }

        OpenImage(path);
    }

    internal void OpenImage(string path, bool resetMarkup = true)
    {
        if (!IsSupportedImage(path))
        {
            StatusText.Text = "Selecciona un archivo JPG, PNG, BMP, SVG o WebP.";
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

        if (!switchingCapture) { RememberActiveCapture(); activeCapture = null; captureExportPath = null; }
        if (!TryLoadPhoto(path))
        {
            return;
        }

        CompletePhotoOpen(path, resetMarkup, GetFolderImages(path));
    }

    private void CompletePhotoOpen(string path, bool resetMarkup, string[] images)
    {
        // Local file openings share this completion path (Explorer, dialog, drop and gallery).
        // Capture-history navigation uses ShowSessionCapture and keeps its own tab.
        historyView=false;
        cloudView=false;
        UpdateHistoryView();
        if (resetMarkup)
        {
            toolsOpen = false;
            activeMarkerTool = null;
            panTool = false;
            MoveTool.IsChecked = true;
            Photo.Cursor = null;
            ApplyWorkspaceLayout();
        }
        folderImages = images;
        currentIndex = Array.FindIndex(folderImages, file =>
            StringComparer.OrdinalIgnoreCase.Equals(file, path));
        CloseFolderGrid();
        ShowPosition();
        SetSlideShowPlaying(false);
        UpdateSlideShowAvailability();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        UpdatePastedHitTesting();
        if(HandlePastedImageDragKey(e))return;
        if(cropCommitTimer is not null)
        {
            if(e.Key==Key.Escape) CancelInlineCrop();
            e.Handled=true; return;
        }
        if (e.Key == Key.Escape && markerDrag is { Kind: MarkerKind.Crop })
        {
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }
        if (TryExitZenWithEscape(e)) return;
        if (HandleInlineCropKey(e)) { return; }
        if (HandleGalleryInteractionKey(e)) { return; }
        if (HandleCloudKey(e)) { return; }
        if (HandleWorkspaceKey(e)) { return; }
        if (markerDrag.HasValue)
        {
            if (e.Key == Key.Escape)
            {
                ReleaseMouseCapture();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && (IsFolderGridOpen || (markers.Count == 0 && !CanUndoCrop && !CanUndoPastedDelete)) && recycledPhotos.Count > 0)
        {
            if (!e.IsRepeat) RestorePhoto();
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

        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (e.IsRepeat) return;
            if (!IsFolderGridOpen && HasPendingChanges)
            {
                DiscardPendingChanges();
            }
            else if (!IsFolderGridOpen && (activeCapture is not null || HasUnsavedCapture))
            {
                SelectWindowMoveTool();
            }
            else if (windowedMode.HasValue)
            {
                ToggleFullScreen();
            }
            return;
        }

        if (IsFolderGridOpen)
        {
            HandleGridKey(e);
            return;
        }

        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && (markers.Count > 0 || CanUndoCrop || CanUndoPastedDelete))
        {
            if (!e.IsRepeat)
            {
                UndoWorkspace();
            }

            e.Handled = true;
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        Key pressedKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (modifiers == ModifierKeys.Alt && Photo.Source is not null &&
            pressedKey is Key.D1 or Key.D2 or Key.D3)
        {
            if (!e.IsRepeat)
            {
                SetPhotoPixelScale(pressedKey switch
                {
                    Key.D1 => 0.5,
                    Key.D2 => 1,
                    _ => 1.5
                });
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Z && modifiers == ModifierKeys.None && Photo.Source is not null)
        {
            if (!e.IsRepeat)
            {
                coverPhoto = !coverPhoto;
                UpdatePhotoZoom();
                SetPhotoPan(0, 0);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 && modifiers == ModifierKeys.None && currentIndex >= 0)
        {
            if (!e.IsRepeat)
            {
                RenameCurrentPhoto();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.C && modifiers == ModifierKeys.Control && Photo.Source is BitmapSource bitmap)
        {
            if (!e.IsRepeat)
            {
                try
                {
                    Clipboard.SetImage(FlattenMarkers(bitmap));
                    StatusText.Text = "Imagen copiada al portapapeles.";
                    MinimizeAfterSuccessfulCopy();
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
                ToggleViewerBackground();
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

    private void RenameCurrentPhoto()
    {
        if (TryBeginGalleryRename()) return;
        SetSlideShowPlaying(false);
        var dialog = new RenameWindow(folderImages[currentIndex]) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.RenamedPath is not { } path)
        {
            return;
        }

        gallerySignature = null;
        folderImages = GetFolderImages(path);
        currentIndex = Array.FindIndex(folderImages, file => StringComparer.OrdinalIgnoreCase.Equals(file, path));
        Title = $"IMG Viewer — {Path.GetFileName(path)}";
        ShowPosition();
        UpdateSlideShowAvailability();
        StatusText.Text = "Archivo renombrado.";
    }

    private void RecycleCurrentPhoto()
    {
        string path = folderImages[currentIndex];
        try
        {
            recycledPhotos.Push(RecycledPhoto.Recycle(path, new WindowInteropHelper(this).Handle));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or OperationCanceledException)
        {
            StatusText.Text = "No se pudo enviar la imagen a la Papelera.";
            return;
        }

        RefreshViewerAfterRecycling(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path });
        StatusText.Text = string.Empty;
        ShowRecycleNotice();
    }

    private void RefreshViewerAfterRecycling(HashSet<string> removed)
    {
        gallerySignature = null;
        if (currentIndex < 0)
        {
            return;
        }

        string previousPath = folderImages[currentIndex];
        int index = folderImages.Take(currentIndex).Count(file => !removed.Contains(file));
        folderImages = folderImages.Where(file => !removed.Contains(file)).ToArray();
        if (!removed.Contains(previousPath))
        {
            currentIndex = index;
            UpdateMetadataFooter();
        }
        else if (!NavigateFrom(index, 1) && !NavigateFrom(index - 1, -1))
        {
            currentIndex = -1;
            lastSavedPhoto = null;
            rotationBasePhoto = null;
            hasPixelEdit = false;
            CropButton.Visibility = Visibility.Collapsed;
            ClearMarkers();
            HideCaptureFrame();
            Photo.Source = null;
            ResetPhotoView();
            SetRotationDegrees(0);
            EmptyState.Visibility = Visibility.Visible;
            UpdateMetadataFooter();
            Title = "IMG Viewer";
        }

        if (IsFolderGridOpen)
        {
            CropButton.Visibility = Visibility.Collapsed;
        }

        UpdateSlideShowAvailability();
    }

    private void ShowRecycleNotice() => UpdateToolbarActions();
    private void RestorePhotoButton_MouseEnter(object sender, MouseEventArgs e) { }
    private void RestorePhotoButton_MouseLeave(object sender, MouseEventArgs e) { }
    private void HideRecycleNotice() => UpdateToolbarActions();
    private void RestorePhotoButton_Click(object sender, RoutedEventArgs e) => UndoWorkspace();

    private void RestorePhoto()
    {
        if (!recycledPhotos.TryPeek(out var photo))
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
            recycledPhotos.Pop();
            HideRecycleNotice();
            bool wasGrid = IsFolderGridOpen;
            string folder = GridFolderPath.Text;
            OpenImage(photo.OriginalPath, resetMarkup: false);
            if (wasGrid) { ToggleFolderGrid(); LoadFolderGrid(folder); }
            if (recycledPhotos.Count > 0) ShowRecycleNotice();
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

    private void OpenCropEditor(object sender, RoutedEventArgs e)
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

        ApplyPixelEdit(result);
    }

    private void ResizeImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (Photo.Source is not BitmapSource bitmap)
        {
            return;
        }

        SetSlideShowPlaying(false);
        var editor = new ResizeImageWindow(bitmap.PixelWidth, bitmap.PixelHeight) { Owner = this };
        if (editor.ShowDialog() != true || editor.TargetSize == (bitmap.PixelWidth, bitmap.PixelHeight))
        {
            return;
        }

        try
        {
            var (width, height) = editor.TargetSize;
            ApplyPixelEdit(ResizeBitmap(FlattenMarkers(bitmap), width, height));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ExternalException or OutOfMemoryException)
        {
            StatusText.Text = "No se pudo redimensionar la imagen.";
        }
    }

    private static BitmapSource ResizeBitmap(BitmapSource bitmap, int width, int height)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawImage(bitmap, new Rect(0, 0, width * 96.0 / bitmap.DpiX, height * 96.0 / bitmap.DpiY));
        }

        var result = new RenderTargetBitmap(width, height, bitmap.DpiX, bitmap.DpiY, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    private void ApplyPixelEdit(BitmapSource result)
    {
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
        ScheduleCaptureDraftSave();
        UpdateToolbarActions();
    }

    private void SavePendingButton_Click(object sender, RoutedEventArgs e)
    {
        SavePhoto(saveCopy: false);
    }

    private void SavePhoto(bool saveCopy)
    {
        EndInlineText(true);
        if (inlineCropPending) { ApplyInlineCrop(); if (inlineCropPending) return; }
        if (Photo.Source is not BitmapSource bitmap)
        {
            return;
        }

        string? originalPath = currentIndex >= 0 ? folderImages[currentIndex] : null;
        string destination;
        bool directCapture = !saveCopy && originalPath is null;
        bool chooseDestination = saveCopy || originalPath is not null && IsSvg(originalPath);
        if (chooseDestination)
        {
            var dialog = new SaveFileDialog
            {
                Title = saveCopy ? "Guardar copia" : "Guardar captura",
                InitialDirectory = originalPath is null ? CapturePreferences.CaptureFolder : Path.GetDirectoryName(originalPath),
                FileName = originalPath is null
                    ? CapturePreferences.FileName(DateTime.Now)
                    : Path.GetFileNameWithoutExtension(originalPath) + " - copia",
                DefaultExt = originalPath is null ? CapturePreferences.FormatExtension : IsSvg(originalPath) ? ".png" : Path.GetExtension(originalPath),
                Filter = "PNG|*.png|JPG|*.jpg;*.jpeg|WebP|*.webp|BMP|*.bmp",
                FilterIndex = originalPath is null ? CapturePreferences.FormatExtension switch { ".png" => 1, ".webp" => 3, ".bmp" => 4, _ => 2 } : IsWebP(originalPath) ? 3 : IsSvg(originalPath) ? 1 : 2,
                AddExtension = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            destination = dialog.FileName;
            if (!SupportedExtensions.Contains(Path.GetExtension(destination)) || IsSvg(destination) || StringComparer.OrdinalIgnoreCase.Equals(destination, originalPath))
            {
                StatusText.Text = "Elige otro archivo con extensión JPG, PNG, BMP, SVG o WebP.";
                return;
            }
        }
        else if (directCapture)
        {
            destination = Path.Combine(CapturePreferences.CaptureFolder,CapturePreferences.FileName(activeCapture?.Created ?? DateTime.Now));
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
            if(directCapture)
                destination=SaveCaptureFile(bitmap,CapturePreferences.CaptureFolder,activeCapture?.Created ?? DateTime.Now);
            else WritePhoto(bitmap, destination, chooseDestination);
            if (!saveCopy)
            {
                ClearMarkers();
                Photo.Source = bitmap;
                lastSavedPhoto = bitmap;
                rotationBasePhoto = bitmap;
                hasPixelEdit = false;
                if (originalPath is null || IsSvg(originalPath))
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

            if (activeCapture is not null || originalPath is null) captureExportPath = destination;
            RememberActiveCapture();
            UpdateCaptureDestination();
            StatusText.Text = saveCopy ? "Copia guardada." : "Cambios guardados.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            StatusText.Text = $"No se pudo guardar {Path.GetFileName(destination)}.";
        }
    }

    private static void WritePhoto(BitmapSource bitmap, string destination, bool writeNewDestination, bool overwrite = true)
    {
        bool webp = IsWebP(destination);
        BitmapEncoder? encoder = webp ? null : Path.GetExtension(destination).ToLowerInvariant() switch
        {
            ".png" => new PngBitmapEncoder(),
            ".bmp" => new BmpBitmapEncoder(),
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            _ => throw new NotSupportedException("Formato de guardado no disponible.")
        };
        if(encoder is JpegBitmapEncoder)
        {
            var visual=new DrawingVisual();
            using(var drawing=visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,bitmap.Width,bitmap.Height));
                drawing.DrawImage(bitmap,new Rect(0,0,bitmap.Width,bitmap.Height));
            }
            var opaque=new RenderTargetBitmap(bitmap.PixelWidth,bitmap.PixelHeight,bitmap.DpiX,bitmap.DpiY,PixelFormats.Pbgra32);
            opaque.Render(visual);opaque.Freeze();bitmap=opaque;
        }
        encoder?.Frames.Add(BitmapFrame.Create(bitmap));

        // Encode beside the destination before replacing any existing file.
        string temporaryPath = Path.Combine(Path.GetDirectoryName(destination)!, Path.GetRandomFileName());
        var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write);
        try
        {
            using (stream)
            {
                if (webp)
                {
                    EncodeWebP(bitmap, stream);
                }
                else
                {
                    encoder!.Save(stream);
                }
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
        photoZoomMultiplier = 1;
        UpdatePhotoZoom();
        SetPhotoPan(0, 0);
    }

    private void UpdatePhotoZoom()
    {
        double fitScale = 1;
        if (coverPhoto && Photo.Source is { Width: > 0, Height: > 0 } image &&
            PhotoViewport.ActualWidth > 0 && PhotoViewport.ActualHeight > 0)
        {
            double widthRatio = PhotoViewport.ActualWidth / image.Width;
            double heightRatio = PhotoViewport.ActualHeight / image.Height;
            fitScale = Math.Max(widthRatio, heightRatio) / Math.Min(widthRatio, heightRatio);
        }

        double scale = fitScale * photoZoomMultiplier;
        PhotoZoom.ScaleX = scale;
        PhotoZoom.ScaleY = scale;
        UpdateZoomFooter();
        SetPhotoPan(PhotoPan.X, PhotoPan.Y);
    }

    private void PhotoViewport_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePhotoZoom();

    private void Photo_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePhotoZoom();

    private void SetPhotoPan(double x, double y)
    {
        double scale = PhotoZoom.ScaleX;
        double maxX = Math.Max(0, (Photo.ActualWidth * scale - PhotoViewport.ActualWidth) / 2);
        double maxY = Math.Max(0, (Photo.ActualHeight * scale - PhotoViewport.ActualHeight) / 2);
        PhotoPan.X = Math.Clamp(x, -maxX, maxX);
        PhotoPan.Y = Math.Clamp(y, -maxY, maxY);
        UpdateCaptureFrame();
        UpdateCurveHandle();
        UpdateInlineCropAdorners();
        UpdatePastedImageAdorners();
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!PhotoViewport.IsMouseOver) return;
        if (IsFolderGridOpen)
        {
            StopGridSelectionScroll();
            return;
        }

        if (markerDrag.HasValue || windowDragOffset.HasValue || photoDragPosition.HasValue || e.Delta == 0 || Photo.Source is null)
        {
            return;
        }

        e.Handled = true;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            double factor = Math.Pow(1.2, e.Delta / 120.0);
            photoZoomMultiplier = Math.Clamp(photoZoomMultiplier * factor, 0.1, 8);
            UpdatePhotoZoom();
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
        SupportedExtensions.Contains(Path.GetExtension(path)) && ViewerPreferences.EnabledExtensions.Contains(Path.GetExtension(path));

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

    private static BitmapSource ReadPhotoBitmap(string path, int decodeWidth = 0)
    {
        if (IsWebP(path)) return ReadWebPBitmap(path, decodeWidth);
        if (IsSvg(path)) return ReadSvgBitmap(path, decodeWidth);
        using var stream = File.OpenRead(path);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = decodeWidth;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void ApplyDecodedPhoto(string path, BitmapSource bitmap)
    {
        StopGridPhotoAnimation();
        HideCaptureFrame();

        ClearMarkers();
        lastSavedPhoto = bitmap;
        rotationBasePhoto = bitmap;
        hasPixelEdit = false;
        SetRotationDegrees(0);
        CropButton.Visibility = Visibility.Visible;
        Photo.Source = bitmap;
        ResetPhotoView();
        EmptyState.Visibility = Visibility.Collapsed;
        Title = $"IMG Viewer — {Path.GetFileName(path)}";
    }

    private bool TryLoadPhoto(string path)
    {
        imageOpenVersion++;
        deferImageDetails = false;
        try
        {
            BitmapSource bitmap = ReadPhotoBitmap(path);
            ApplyDecodedPhoto(path, bitmap);
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
        UpdateZoomFooter();
        if (deferImageDetails)
        {
            if (Photo.Source is BitmapSource pending) SetImageInfo($"{Path.GetFileName(CurrentGridImage)} · {pending.PixelWidth} × {pending.PixelHeight} px");
            UpdateToolbarActions();
            return;
        }
        RefreshGallery();
        UpdateToolbarActions();
        UpdateCaptureDestination();
        if (MetadataFooter.Visibility != Visibility.Visible)
        {
            return;
        }

        if (Photo.Source is not BitmapSource bitmap)
        {
            SetImageInfo("No hay una imagen abierta.");
            return;
        }

        if (HasUnsavedCapture)
        {
            SetImageInfo($"Captura de pantalla · {bitmap.PixelWidth} × {bitmap.PixelHeight} px · Sin guardar");
            return;
        }

        SetImageInfo(DescribePhotoFile(folderImages[currentIndex],
            $"{currentIndex + 1} / {folderImages.Length}", bitmap));
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        >= 1073741824 => $"{bytes / 1073741824.0:0.##} GiB",
        >= 1048576 => $"{bytes / 1048576.0:0.##} MiB",
        >= 1024 => $"{bytes / 1024.0:0.##} KiB",
        _ => $"{bytes} B"
    };

    private static string DescribePhotoFile(string path, string? position = null, BitmapSource? displayed = null)
    {
        var fields = new List<string> { Path.GetFileName(path) };
        if (position is not null)
        {
            fields.Add(position);
        }

        if (displayed is not null)
        {
            fields.Add($"{displayed.PixelWidth} × {displayed.PixelHeight} px");
        }

        try
        {
            using var stream = File.OpenRead(path);
            fields.Add(FormatFileSize(stream.Length));
            if (IsSvg(path) || IsWebP(path))
            {
                if (displayed is null)
                {
                    BitmapSource svg = ReadPhotoBitmap(path);
                    fields.Add($"{svg.PixelWidth} × {svg.PixelHeight} px");
                }
                fields.Add(IsSvg(path) ? "SVG" : "WebP");
                return string.Join("  ·  ", fields);
            }

            BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            if (displayed is null)
            {
                fields.Add($"{frame.PixelWidth} × {frame.PixelHeight} px");
            }

            if (frame.Metadata is BitmapMetadata metadata)
            {
                if (!string.IsNullOrWhiteSpace(metadata.DateTaken))
                {
                    fields.Add($"Captura: {metadata.DateTaken}");
                }

                if (!string.IsNullOrWhiteSpace(metadata.CameraModel))
                {
                    fields.Add($"Cámara: {metadata.CameraModel}");
                }

                object? iso = metadata.GetQuery("/app1/ifd/exif/{ushort=34855}");
                if (iso is ushort or uint)
                {
                    fields.Add($"ISO {iso}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   NotSupportedException or FormatException or COMException or ArgumentException)
        {
            // Keep the available fields when a file or metadata block cannot be read.
        }

        return string.Join("  ·  ", fields);
    }
}
