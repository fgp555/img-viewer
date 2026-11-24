using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private bool zenMode;
    private bool galleryOpen;
    private bool toolsOpen;
    private double galleryWidth = 180;
    private double toolsWidth = 210;
    private MarkerKind? activeMarkerTool;
    private Brush markerBrush = CreateMarkerBrush("violet");
    private CancellationTokenSource? galleryLoad;
    private string? gallerySignature;
    private bool syncingGallery;
    private string? browsedFolder;
    private string? gallerySearchRoot;
    private readonly System.Windows.Threading.DispatcherTimer gallerySearchTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    private void InitializeWorkspace()
    {
        using var settings = Registry.CurrentUser.OpenSubKey(@"Software\IMGViewer");
        galleryOpen = settings?.GetValue("GalleryOpen") is 1;
        toolsOpen = settings?.GetValue("ToolsOpen") is 1;
        if (settings?.GetValue("GalleryWidth") is int left) galleryWidth = Math.Clamp(left, 140, 360);
        if (settings?.GetValue("ToolsWidth") is int right) toolsWidth = Math.Clamp(right, 170, 360);
        gallerySearchTimer.Tick += (_, _) => { gallerySearchTimer.Stop(); RefreshGallery(true); };
        StateChanged += (_, _) => { UpdateCaptionIcon(); UpdateNativeWindowFrame(); };
        LoadGalleryFolders();
        Loaded += (_, _) => UpdateMarkerColorChecks();
        Loaded += (_, _) => { ApplyWorkspaceLayout(); RefreshGallery(); };
        SizeChanged += (_, _) => ApplyWorkspaceLayout();
        Closed += (_, _) => { gallerySearchTimer.Stop(); galleryLoad?.Cancel(); galleryLoad?.Dispose(); };
    }

    private void ApplyWorkspaceLayout()
    {
        if (Workspace is null) return;
        bool hideChrome=zenMode || windowedMode.HasValue;
        MainWindowFrame.BorderThickness = new Thickness(0);
        MainWindowFrame.CornerRadius = new CornerRadius(0);
        Workspace.Margin = new Thickness(0);
        Workspace.Clip = null;
        TitleBar.Visibility = ActionBar.Visibility = hideChrome ? Visibility.Collapsed : Visibility.Visible;
        UpdateWindowCornerClip();
        ApplyCompactMode();
        // Preserve the image's working space on compact windows, without losing panel preferences.
        bool showTools = !hideChrome && toolsOpen && ActualWidth >= 560;
        bool showGallery = !hideChrome && galleryOpen && ActualWidth >= (showTools ? 840 : 520);
        GalleryColumn.Width = new GridLength(showGallery ? galleryWidth : 0);
        GalleryColumn.MinWidth = showGallery ? 140 : 0;
        GalleryColumn.MaxWidth = showGallery ? Math.Min(360, Math.Max(140, ActualWidth - 360 - (showTools ? toolsWidth : 0))) : 0;
        ToolsColumn.Width = new GridLength(showTools ? toolsWidth : 0);
        ToolsColumn.MinWidth = showTools ? 170 : 0;
        ToolsColumn.MaxWidth = showTools ? Math.Min(360, Math.Max(170, ActualWidth - 360)) : 0;
        GallerySplitterColumn.Width = new GridLength(showGallery ? 4 : 0);
        ToolsSplitterColumn.Width = new GridLength(showTools ? 4 : 0);
        Photo.Cursor = !hideChrome && panTool ? Cursors.Hand : !hideChrome && activeMarkerTool is not null ? Cursors.Cross : null;
        GalleryPanel.Visibility = showGallery ? Visibility.Visible : Visibility.Collapsed;
        ToolsPanel.Visibility = showTools ? Visibility.Visible : Visibility.Collapsed;
        ImageFooter.Visibility=hideChrome?Visibility.Collapsed:Visibility.Visible;
        ImageFooter.Opacity = hideChrome ? 0 : 1;
        ImageFooter.IsHitTestVisible = !hideChrome;
        GridFooter.Visibility = !hideChrome && IsFolderGridOpen ? Visibility.Visible : Visibility.Collapsed;
        StatusRow.Height = hideChrome ? new GridLength(0) : GridLength.Auto;
        // The transparent background remains limited to the image canvas.
        Workspace.Background = zenMode ? Brushes.Transparent : (Brush)FindResource("ViewerBackground");
        ApplyCompactPanels();
        UpdateCurveHandle();
        UpdateInlineCropAdorners();
        UpdatePastedImageAdorners();
        if (showGallery) RefreshGallery();
    }

    private void PersistPanels()
    {
        using var settings = Registry.CurrentUser.CreateSubKey(@"Software\IMGViewer");
        settings.SetValue("GalleryOpen", galleryOpen ? 1 : 0);
        settings.SetValue("ToolsOpen", toolsOpen ? 1 : 0);
        settings.SetValue("GalleryWidth", (int)galleryWidth);
        settings.SetValue("ToolsWidth", (int)toolsWidth);
    }

    private void Sidebar_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (GalleryColumn.ActualWidth > 0) galleryWidth = GalleryColumn.ActualWidth;
        if (ToolsColumn.ActualWidth > 0) toolsWidth = ToolsColumn.ActualWidth;
        PersistPanels();
    }

    private sealed record ModeWindowPlacement(Rect Bounds, WindowState State);
    private ModeWindowPlacement? normalPlacement;
    private ModeWindowPlacement? zenPlacement;

    private void ToggleZen() => SetZenMode(!zenMode);

    private void SetZenMode(bool enabled)
    {
        if (zenMode == enabled) return;
        // Fullscreen has its own restore rectangle; leave it before storing either mode.
        if (windowedMode.HasValue) ToggleFullScreen();
        Rect bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;
        var current = new ModeWindowPlacement(bounds, WindowState == WindowState.Minimized ? WindowState.Normal : WindowState);
        if (zenMode) zenPlacement = current;
        else normalPlacement = current;
        ModeWindowPlacement target = (enabled ? zenPlacement : normalPlacement) ?? current;

        viewerClickTimer.Stop();
        if (markerDrag.HasValue) ReleaseMouseCapture();
        WindowState = WindowState.Normal;
        zenMode = enabled;
        ApplyWorkspaceLayout();
        if (!target.Bounds.IsEmpty)
        {
            Left = target.Bounds.Left;
            Top = target.Bounds.Top;
            Width = Math.Max(MinWidth, target.Bounds.Width);
            Height = Math.Max(MinHeight, target.Bounds.Height);
        }
        WindowState = target.State;
        UpdateLayout();
        Keyboard.Focus(this);
    }

    private void GalleryToggle_Click(object sender, RoutedEventArgs e)
    {
        galleryOpen = !galleryOpen;
        SetZenMode(false);
        ApplyWorkspaceLayout();
        PersistPanels();
        if (ActualWidth < 640 && galleryOpen) { toolsOpen = false; ApplyCompactPanels(); }
    }

    private void ToolsToggle_Click(object sender, RoutedEventArgs e)
    {
        toolsOpen = !toolsOpen;
        SetZenMode(false);
        ApplyWorkspaceLayout();
        PersistPanels();
        if (ActualWidth < 640 && toolsOpen) { galleryOpen = false; ApplyCompactPanels(); }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != TitleBar) return;
        if (e.ClickCount == 2) MaximizeWorkspace_Click(sender, e);
        else if (windowedMode is null) DragMove();
        e.Handled = true;
    }

    private void MaximizeWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (windowedMode is not null) ToggleFullScreen();
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void SelectElementMoveTool()
    {
        if (Keyboard.FocusedElement is ComboBox picker) picker.IsDropDownOpen = false;
        if (markerDrag.HasValue || photoDragPosition.HasValue || windowDragOffset.HasValue) ReleaseMouseCapture();
        activeMarkerTool = null;
        panTool = false;
        MoveTool.IsChecked = true;
        Photo.Cursor = null;
        ClearValue(CursorProperty);
        ClearValue(ForceCursorProperty);
        Keyboard.Focus(this);
        UpdatePastedImageAdorners();
    }

    private bool HandleWorkspaceKey(KeyEventArgs e)
    {
        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is not TextBoxBase &&
            !(Keyboard.FocusedElement is ComboBox { IsEditable: true, IsKeyboardFocusWithin: true }) &&
            !MainMenu.Items.OfType<MenuItem>().Any(item => item.IsSubmenuOpen) && Workspace.ContextMenu?.IsOpen != true)
        {
            if (!e.IsRepeat)
            {
                SelectElementMoveTool();
            }
            e.Handled = true;
            return true;
        }
        // Do not consume typing or native menu/navigation shortcuts.
        if (Keyboard.FocusedElement is TextBoxBase or ComboBox ||
            MainMenu.Items.OfType<MenuItem>().Any(item => item.IsSubmenuOpen) ||
            (MainMenu.IsKeyboardFocusWithin && Keyboard.Modifiers == ModifierKeys.None) ||
            Workspace.ContextMenu?.IsOpen == true) return true;
        if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control)
        {if(!e.IsRepeat)UndoWorkspace();e.Handled=true;return true;}
        if(e.Key==Key.Y&&Keyboard.Modifiers==ModifierKeys.Control || e.Key==Key.Z&&Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift))
        {if(!e.IsRepeat)RedoWorkspace();e.Handled=true;return true;}
        if(e.Key==Key.D&&Keyboard.Modifiers==ModifierKeys.Control)
        {if(!e.IsRepeat)DuplicateSelectedElement();e.Handled=true;return true;}
        if(e.Key==Key.Enter&&Keyboard.Modifiers==ModifierKeys.None&&HasSelectedEditableMarker&&markers[selectedPastedImage].Definition?.Kind==MarkerKind.Text)
        {EditSelectedText();e.Handled=true;return true;}
        if(e.Key==Key.E&&Keyboard.Modifiers==ModifierKeys.None&&Photo.Source is not null&&!IsFolderGridOpen)
        {EllipseTool.IsChecked=true;toolsOpen=true;SetZenMode(false);ApplyWorkspaceLayout();e.Handled=true;return true;}
        if(e.Key==Key.Delete && Keyboard.Modifiers==ModifierKeys.None && selectedPastedImage>=0 && !IsFolderGridOpen)
        {
            if(!e.IsRepeat)DeleteSelectedPastedImage();e.Handled=true;return true;
        }
        if(e.Key==Key.V && Keyboard.Modifiers==ModifierKeys.Control)
        {
            e.Handled=true;if(!e.IsRepeat)PasteClipboardImage();return true;
        }
        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None &&
            (zenMode || Keyboard.FocusedElement == this || PhotoViewport.IsMouseOver))
        {
            if (!e.IsRepeat) ToggleZen();
            e.Handled = true;
            return true;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.T or Key.B && Photo.Source is not null && !IsFolderGridOpen)
        {
            if (!e.IsRepeat) ActivateExtraMarker(e.Key == Key.T ? MarkerKind.Text : MarkerKind.Blur);
            e.Handled = true; return true;
        }
        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control && IsFolderGridOpen)
        {
            SelectAllGridImages(true); e.Handled = true; return true;
        }
        if (e.Key == Key.O && Keyboard.Modifiers is ModifierKeys.Control or (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (!e.IsRepeat)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) OpenWorkspaceFolder();
                else OpenWorkspaceImage();
            }
            e.Handled = true;
            return true;
        }
        return false;
    }

    private void WorkspaceContext_Opened(object sender, RoutedEventArgs e) => UpdateActionStates((ItemsControl)sender);
    private void Menus_Opened(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is MenuItem menu) UpdateActionStates(menu);
    }

    private void UpdateActionStates(ItemsControl parent)
    {
        foreach (var item in parent.Items.OfType<MenuItem>())
        {
            string action = item.Tag as string ?? "";
            bool image = Photo.Source is BitmapSource && !IsFolderGridOpen;
            item.IsEnabled = action switch
            {
                "save" or "savecopy" or "rotate" or "rotateback" or "crop" or "clear" => image,
                "copy" or "resize" or "convert" or "delete" => IsFolderGridOpen ? SelectedGridPaths().Length > 0 : image,
                "cloud" => cloudSettings.Enabled && !cloudBusy && (IsFolderGridOpen ? SelectedGridPaths().Length > 0 : image),
                "quickwebp" or "quickjpg" => !quickConversionInProgress && !gridBatchInProgress && (IsFolderGridOpen ? SelectedGridPaths().Length > 0 : image),
                "redo" => CanRedoEdit,
                "duplicate" => HasSelectedEditableMarker,
                "edittext" => HasSelectedEditableMarker&&markers[selectedPastedImage].Definition?.Kind==MarkerKind.Text,
                "paste" => !IsFolderGridOpen && !gridBatchInProgress,
                "rename" => image && currentIndex >= 0,
                "undo" => image && (markers.Count > 0 || CanUndoCrop || CanUndoPastedDelete || CanUndoElementEdit) || recycledPhotos.Count > 0,
                _ => true
            };
            if(action=="delete")item.Header=HasSelectedEditableMarker?"Eliminar elemento":"Enviar a la Papelera";
            if(action=="cloud")item.Visibility=cloudSettings.Enabled?Visibility.Visible:Visibility.Collapsed;
            if (item.IsCheckable) item.IsChecked = action switch
            {
                "zen" => zenMode,
                "gallery" => galleryOpen,
                "tools" => toolsOpen,
                "info" => MetadataFooter.Visibility == Visibility.Visible,
                _ => false
            };
        }
    }

    private void WorkspaceAction_Click(object sender, RoutedEventArgs e)
    {
        string? action = (sender as FrameworkElement)?.Tag as string;
        switch (action)
        {
            case "zen": ToggleZen(); break;
            case "previous": case "next":
                if (currentIndex >= 0 && ConfirmWorkspaceNavigation()) { CloseFolderGrid(); Navigate(action == "previous" ? -1 : 1); }
                break;
            case "open": OpenWorkspaceImage(); break;
            case "folder": OpenWorkspaceFolder(); break;
            case "save": SavePhoto(false); break;
            case "savecopy": SavePhoto(true); break;
            case "convert": if (IsFolderGridOpen) ConvertSelectedGridPhotos(); else SavePhoto(true); break;
            case "quickwebp": QuickConvertPhoto(".webp"); break;
            case "quickjpg": QuickConvertPhoto(".jpg"); break;
            case "copy": CopyWorkspaceImage(); break;
            case "paste": PasteClipboardImage(); break;
            case "minimize": WindowState = WindowState.Minimized; break;
            case "normalmode": SetZenMode(false); break;
            case "close": Close(); break;
            case "exit": if (Application.Current is App app) app.ExitViewer(); break;
            case "clear": ClearWorkspaceImage(); break;
            case "restore": RestorePhoto(); break;
            case "selectall": if (IsFolderGridOpen) SelectAllGridImages(true); break;
            case "scale50": SetPhotoPixelScale(0.5); break;
            case "scale100": SetPhotoPixelScale(1); break;
            case "scale150": SetPhotoPixelScale(1.5); break;
            case "zoomin": case "zoomout": if (Photo.Source is not null) { photoZoomMultiplier = Math.Clamp(photoZoomMultiplier * (action == "zoomin" ? 1.2 : 1 / 1.2), 0.1, 8); UpdatePhotoZoom(); } break;
            case "cover": coverPhoto = !coverPhoto; UpdatePhotoZoom(); SetPhotoPan(0,0); break;
            case "discard": if (HasPendingChanges) DiscardPendingChanges(); break;
            case "topmost": Topmost = !Topmost; break;
            case "fit": coverPhoto = false; ResetPhotoView(); break;
            case "freehandtool": ActivateExtraMarker(MarkerKind.Freehand); break;
            case "texttool": ActivateExtraMarker(MarkerKind.Text); break;
            case "blurtool": ActivateExtraMarker(MarkerKind.Blur); break;
            case "curvedarrowtool": ArrowFormPicker.SelectedIndex = 2; goto case "arrowtool";
            case "arrowtool": ArrowTool.IsChecked = true; activeMarkerTool = MarkerKind.Arrow; panTool = false; toolsOpen = true; SetZenMode(false); ApplyWorkspaceLayout(); break;
            case "rectangletool": RectangleTool.IsChecked = true; activeMarkerTool = MarkerKind.Rectangle; panTool = false; toolsOpen = true; SetZenMode(false); ApplyWorkspaceLayout(); break;
            case "pan": activeMarkerTool = null; panTool = true; Photo.Cursor = Cursors.Hand; break;
            case "movetool": activeMarkerTool = null; panTool = false; MoveTool.IsChecked = true; Photo.Cursor = null; break;
            case "capture": Dispatcher.InvokeAsync(CaptureScreen, System.Windows.Threading.DispatcherPriority.Background); break;
            case "gallery": GalleryToggle_Click(sender, e); break;
            case "tools": ToolsToggle_Click(sender, e); break;
            case "grid": ToggleFolderGrid(); break;
            case "fullscreen": ToggleFullScreen(); break;

            case "settings": CaptureSettingsButton_Click(sender, e); break;
            case "cloud": CloudUpload_Click(sender,e); break;
            case "undo": UndoWorkspace(); break;
            case "redo": RedoWorkspace(); break;
            case "duplicate": DuplicateSelectedElement(); break;
            case "edittext": EditSelectedText(); break;
            case "ellipsetool": EllipseTool.IsChecked=true;toolsOpen=true;SetZenMode(false);ApplyWorkspaceLayout();break;
            case "rename": if (currentIndex >= 0) RenameCurrentPhoto(); break;
            case "rotate": RotatePhoto(90); break;
            case "rotateback": RotatePhoto(-90); break;
            case "inlinecrop": CropButton_Click(sender, e); break;
            case "crop": OpenCropEditor(sender, e); break;
            case "resize": if (IsFolderGridOpen) GridResize_Click(sender, e); else ResizeImageButton_Click(sender, e); break;
            case "delete": if (IsFolderGridOpen) RecycleGridFiles(); else if (!DeleteSelectedPastedImage() && currentIndex >= 0) RecycleCurrentPhoto(); break;
            case "background":
                ToggleViewerBackground();
                break;
            case "shortcuts": ShowActionButtons(); break;
            case "about": ViewerDialog.Show(this, "IMG Viewer\nVersión 2026.11.1\nVisor, capturas y marcación de imágenes.\nfrankgp.com/img-viewer", "Acerca de IMG Viewer"); break;
            case "website": Process.Start(new ProcessStartInfo("https://frankgp.com/img-viewer") { UseShellExecute = true }); break;
        }
        e.Handled = true;
    }

    private void MinimizeAfterSuccessfulCopy()
    {
        if (ViewerPreferences.MinimizeAfterCopy) WindowState = WindowState.Minimized;
    }

    private void CopyWorkspaceImage()
    {
        if (IsFolderGridOpen) { CopyGridFiles(); return; }
        if (Photo.Source is not BitmapSource bitmap) return;
        try { Clipboard.SetImage(FlattenMarkers(bitmap)); StatusText.Text = "Imagen copiada al portapapeles."; MinimizeAfterSuccessfulCopy(); }
        catch (ExternalException) { StatusText.Text = "No se pudo copiar la imagen. Vuelve a intentarlo."; }
    }

    private void OpenWorkspaceImage()
    {
        if (ViewerPreferences.EnabledExtensions.Count == 0) { StatusText.Text = "Selecciona al menos un tipo de archivo en Configuración > Galería."; return; }
        string patterns = string.Join(";", ViewerPreferences.EnabledExtensions.Select(ext => "*" + ext));
        var dialog = new OpenFileDialog { Title = "Abrir imagen", Filter = "Imágenes|" + patterns };
        if (dialog.ShowDialog(this) == true && ConfirmWorkspaceNavigation()) OpenImage(dialog.FileName);
    }

    internal void OpenSettingsFromTray() => CaptureSettingsButton_Click(this, new RoutedEventArgs());

    internal void OpenFolderFromTray() => OpenWorkspaceFolder();

    private void OpenWorkspaceFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Abrir carpeta de imágenes" };
        if (dialog.ShowDialog(this) != true || !ConfirmWorkspaceNavigation()) return;
        browsedFolder = dialog.FolderName;
        SetZenMode(false);
        galleryOpen = true;
        ApplyWorkspaceLayout();
        if (!IsFolderGridOpen) ToggleFolderGrid();
        LoadFolderGrid(browsedFolder);
        RefreshGallery(true);
        PersistPanels();
    }

    private bool ConfirmWorkspaceNavigation() => !HasPendingChanges || ViewerDialog.Show(this,
        "Hay cambios sin guardar. ¿Quieres descartarlos y continuar?", "IMG Viewer",
        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void ClearWorkspaceImage()
    {
        imageOpenVersion++;
        deferImageDetails = false;
        if (activeCapture is null && !ConfirmWorkspaceNavigation()) return;
        EndInlineText(true);
        RememberActiveCapture();
        draftSaveTimer.Stop();activeCapture=null;captureExportPath=null;
        SetSlideShowPlaying(false);
        StopGridPhotoAnimation();
        HideCaptureFrame();
        CancelInlineCrop();
        ClearMarkers();
        Photo.Source = null;
        lastSavedPhoto = rotationBasePhoto = null;
        hasPixelEdit = false;
        rotationDegrees = 0;
        folderImages = [];
        currentIndex = -1;
        EmptyState.Visibility = Visibility.Visible;
        CropButton.Visibility = Visibility.Collapsed;
        Title = "IMG Viewer";
        UpdatePendingSaveIndicator();
        UpdateSlideShowAvailability();
        UpdateMetadataFooter();
    }

    private void MarkerTool_Checked(object sender, RoutedEventArgs e)
    {
        panTool = false;
        activeMarkerTool = ((sender as FrameworkElement)?.Tag as string) switch
        {
            "arrow" => MarkerKind.Arrow,
            "rectangle" => MarkerKind.Rectangle,
            "ellipse" => MarkerKind.Ellipse,
            "freehand" => MarkerKind.Freehand,
            "text" => MarkerKind.Text,
            "blur" => MarkerKind.Blur,
            _ => null
        };
        SyncMarkerWidthControl();
        if (Photo is not null) Photo.Cursor = activeMarkerTool is null ? null : Cursors.Cross;
        UpdatePastedImageAdorners();SyncSelectedElementProperties();
    }

    private void UpdateMarkerColorChecks()
    {
        if (MarkerColors is null) return;
        foreach (var button in MarkerColors.Children.OfType<Button>())
        {
            bool selected = markerColorKey == (string)button.Tag;
            button.Content = selected ? new TextBlock { Text = "\uE73E", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, Foreground = markerBrush is LinearGradientBrush ? Brushes.White : Brushes.Black } : null;
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, selected ? "Seleccionado" : "");
        }
    }

    private void MarkerColor_Click(object sender, RoutedEventArgs e)
    {
        string color = (string)((Button)sender).Tag;
        markerColorKey=color;
        markerBrush=CreateMarkerBrush(color);
        MarkerColorLabel.Text = "Color: " + ((Button)sender).ToolTip;
        UpdateMarkerColorChecks();EditSelectedElement("color");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowSizingPoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowMinMaxInfo
    {
        public WindowSizingPoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    private bool SetWorkspaceMaximizedBounds(nint handle, nint data)
    {
        if (data == 0) return false;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref info)) return false;
        var limits = Marshal.PtrToStructure<WindowMinMaxInfo>(data);
        limits.MaxPosition.X = info.WorkArea.Left - info.Monitor.Left;
        limits.MaxPosition.Y = info.WorkArea.Top - info.Monitor.Top;
        limits.MaxSize.X = info.WorkArea.Right - info.WorkArea.Left;
        limits.MaxSize.Y = info.WorkArea.Bottom - info.WorkArea.Top;
        var dpi = VisualTreeHelper.GetDpi(this);
        limits.MinTrackSize.X = Math.Max(limits.MinTrackSize.X, (int)Math.Ceiling(MinWidth * dpi.DpiScaleX));
        limits.MinTrackSize.Y = Math.Max(limits.MinTrackSize.Y, (int)Math.Ceiling(MinHeight * dpi.DpiScaleY));
        Marshal.StructureToPtr(limits, data, false);
        return true;
    }

    private void UpdateToolbarActions()
    {
        if (CopyPhotoButton is null || SavePendingButton is null || DeletePhotoButton is null) return;
        UpdateCloudActions();
        RestorePhotoButton.Visibility = Visibility.Visible;
        RestorePhotoButton.IsEnabled = (!IsFolderGridOpen && (markers.Count > 0 || CanUndoCrop || CanUndoPastedDelete || CanUndoElementEdit)) || recycledPhotos.Count > 0;
        bool image = Photo.Source is BitmapSource;
        bool inGrid = IsFolderGridOpen;
        bool selected = inGrid && SelectedGridPaths().Length > 0 && !gridBatchInProgress;
        CopyPhotoButton.IsEnabled = inGrid ? selected : image;
        DeletePhotoButton.IsEnabled = inGrid ? selected : image && (currentIndex >= 0 || HasSelectedEditableMarker);
        DeletePhotoButton.ToolTip=HasSelectedEditableMarker?"Eliminar elemento · Supr":"Enviar imagen a la Papelera · Supr";
        SavePendingButton.IsEnabled = image && !inGrid;
        SavePendingButton.Foreground = HasPendingChanges || HasUnsavedCapture ? Brushes.Orange : Brushes.LightGray;
        SavePendingButton.ToolTip = HasPendingChanges || HasUnsavedCapture ? "Cambios sin guardar · Ctrl + S" : "Guardar · Ctrl + S";
    }

    private void UpdateCaptionIcon()
    {
        if (MaximizeCaptionIcon is null) return;
        MaximizeCaptionIcon.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void GallerySearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (GallerySearch is null || GalleryList is null) return;
        if (!string.IsNullOrWhiteSpace(GallerySearch.Text) && gallerySearchRoot is null)
            gallerySearchRoot = GalleryFolder.Text;
        if (string.IsNullOrWhiteSpace(GallerySearch.Text)) gallerySearchRoot = null;
        galleryLoad?.Cancel();
        gallerySearchTimer.Stop();
        gallerySearchTimer.Start();
    }

    private static GalleryPhoto[] FindGalleryPhotos(string folder, string query, CancellationToken token)
    {
        var result = new List<GalleryPhoto>();
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            try
            {
                foreach (string file in Directory.EnumerateFiles(directory))
                {
                    token.ThrowIfCancellationRequested();
                    if (IsSupportedImage(file) && (query.Length == 0 ||
                        Path.GetRelativePath(folder, file).Contains(query, StringComparison.OrdinalIgnoreCase)))
                        result.Add(new GalleryPhoto(file));
                }
                if (query.Length == 0) continue;
                foreach (string child in Directory.EnumerateDirectories(directory))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        // Do not follow junctions or symlinks outside the selected tree.
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(directory, folder)) throw;
            }
        }
        token.ThrowIfCancellationRequested();
        return result.OrderBy(item => Path.GetRelativePath(folder, item.Path), StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private sealed class GalleryPhoto(string path) : System.ComponentModel.INotifyPropertyChanged
    {
        private string path = path;
        public string Path { get => path; set { path = value; foreach (string name in new[] { nameof(Path), nameof(Name), nameof(DisplayName), nameof(Format) }) PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name)); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public string Name => System.IO.Path.GetFileName(Path);
        public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(Path);
        public string Format => System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
    }

    private async void RefreshGallery(bool force = false)
    {
        if (cloudView || deferImageDetails || GalleryList is null || !galleryOpen || !IsLoaded) return;
        string? current = CurrentGridImage;
        string folder = IsFolderGridOpen && !string.IsNullOrWhiteSpace(GridFolderPath.Text) ? GridFolderPath.Text :
            current is not null ? Path.GetDirectoryName(current)! : browsedFolder ?? CapturePreferences.CaptureFolder;
        folder = selectedGalleryFolder ?? folder;
        UpdateGalleryFolderPicker(folder);
        string query = GallerySearch.Text.Trim();
        if (query.Length > 0 && !string.IsNullOrWhiteSpace(gallerySearchRoot)) folder = gallerySearchRoot;
        string signature = folder + "|" + query;
        if (!force && gallerySignature == signature && GalleryList.ItemsSource is GalleryPhoto[] existing &&
            (current is null || existing.Any(item => StringComparer.OrdinalIgnoreCase.Equals(item.Path, current))))
        {
            syncingGallery = true;
            try
            {
                GalleryList.SelectedItem = existing.FirstOrDefault(item => StringComparer.OrdinalIgnoreCase.Equals(item.Path, current));
                if (galleryRenamePhoto is null && GalleryList.SelectedItem is GalleryPhoto selected) RevealGalleryPhoto(selected);
            }
            finally { syncingGallery = false; }
            return;
        }
        if (!force && gallerySignature == signature && GalleryList.ItemsSource is not GalleryPhoto[]) return;
        gallerySignature = signature;
        galleryLoad?.Cancel();
        galleryLoad?.Dispose();
        galleryLoad = new CancellationTokenSource();
        var token = galleryLoad.Token;
        GalleryFolder.Text = folder;
        GalleryMessage.Text = "Cargando…";
        try
        {
            var photos = await Task.Run(() => FindGalleryPhotos(folder, query, token), token);
            if (token.IsCancellationRequested) return;
            current = CurrentGridImage;
            syncingGallery = true;
            try
            {
                GalleryList.ItemsSource = photos;
                GalleryList.SelectedItem = photos.FirstOrDefault(item => StringComparer.OrdinalIgnoreCase.Equals(item.Path, current));
                if (galleryRenamePhoto is null && GalleryList.SelectedItem is GalleryPhoto selected) RevealGalleryPhoto(selected);
            }
            finally { syncingGallery = false; }
            GalleryMessage.Text = photos.Length == 0 ? (query.Length > 0 ? "Sin resultados en esta carpeta y sus subcarpetas." : "No hay imágenes en esta carpeta.") : $"{photos.Length} {(photos.Length == 1 ? "imagen" : "imágenes")}";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!token.IsCancellationRequested) GalleryMessage.Text = "No se pudo abrir la carpeta.";
        }
    }

    private void GalleryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncingGallery || GalleryList.SelectedItem is not GalleryPhoto photo) return;
        if (ConfirmWorkspaceNavigation()) OpenImage(photo.Path, resetMarkup: false);
        else RefreshGallery(true);
        Keyboard.Focus(GalleryList);
    }

    private void GalleryThumbnail_Loaded(object sender, RoutedEventArgs e) => LoadGalleryThumbnail((Image)sender);
    private void GalleryThumbnail_Changed(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (((Image)sender).IsLoaded) LoadGalleryThumbnail((Image)sender);
    }
    private void GalleryThumbnail_Unloaded(object sender, RoutedEventArgs e) => CancelGalleryThumbnail((Image)sender);
    private static void CancelGalleryThumbnail(Image image)
    {
        if (image.Tag is CancellationTokenSource lifetime) lifetime.Cancel();
        image.Tag = null;
        image.Source = null;
    }
    private async void LoadGalleryThumbnail(Image image)
    {
        CancelGalleryThumbnail(image);
        if (image.DataContext is not GalleryPhoto photo) return;
        using var lifetime = new CancellationTokenSource();
        image.Tag = lifetime;
        var token = lifetime.Token;
        try
        {
            await thumbnailSlots.WaitAsync(token);
            try
            {
                var bitmap = await Task.Run(() => ReadCachedThumbnail(photo.Path, token), token);
                if (!token.IsCancellationRequested && ReferenceEquals(image.Tag, lifetime))
                {
                    image.Source = bitmap;
                    if (image.Parent is Grid card) UpdateGalleryCard(card);
                }
            }
            finally { thumbnailSlots.Release(); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (IsBatchImageError(ex)) { }
        finally { if (ReferenceEquals(image.Tag, lifetime)) image.Tag = null; }
    }
}
