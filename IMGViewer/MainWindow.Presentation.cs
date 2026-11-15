using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private bool panTool;
    private readonly List<string> addedGalleryFolders = [];
    private string? selectedGalleryFolder;
    private bool syncingFolderPicker;
    private sealed record GalleryFolderEntry(string Path) { public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\')) is { Length: > 0 } name ? name : Path; public override string ToString() => Name; }
    private void MainWindowFrame_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateWindowCornerClip();
    private void UpdateWindowCornerClip()
    {
        if (MainWindowFrame is null) return;
        MainWindowFrame.Clip = null;
        UpdateNativeWindowFrame();
    }
    private void Workspace_SizeChanged(object sender, SizeChangedEventArgs e) => Workspace.Clip = null;
    private void UpdateBackgroundToggle()
    {
        if (BackgroundToggleIcon is null) return;
        bool solid = viewerBackgroundMode == 0;
        BackgroundToggleIcon.Text = solid ? "\uF232" : "\uEA3B";
        string label = solid ? "Cambiar a cuadrícula" : "Cambiar a fondo sólido";
        BackgroundToggleButton.ToolTip = label + " · T";
        System.Windows.Automation.AutomationProperties.SetName(BackgroundToggleButton, label);
        BackgroundMenuItem.Header = label;
    }

    private void ToggleViewerBackground()
    {
        viewerBackgroundMode = 1 - viewerBackgroundMode;
        UpdateBackgroundToggle();
        PhotoViewport.Background = (Brush)FindResource(viewerBackgroundMode == 0 ? "ViewerBackground" : "TransparencyBackground");
    }
    private void LoadGalleryFolders()
    {
        using var settings = Registry.CurrentUser.OpenSubKey(@"Software\IMGViewer");
        if (settings?.GetValue("GalleryFolders") is string[] folders) addedGalleryFolders.AddRange(folders.Distinct(StringComparer.OrdinalIgnoreCase));
    }
    private void UpdateGalleryFolderPicker(string folder)
    {
        if (GalleryFolderPicker is null) return;
        string current = CurrentGridImage is { } image ? System.IO.Path.GetDirectoryName(image)! : CapturePreferences.CaptureFolder;
        var folders = new[] { current, folder }.Concat(addedGalleryFolders).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (GalleryFolderPicker.ItemsSource is GalleryFolderEntry[] existing && existing.Select(item => item.Path).SequenceEqual(folders, StringComparer.OrdinalIgnoreCase)) return;
        syncingFolderPicker = true;
        try { GalleryFolderPicker.ItemsSource = folders.Select(path => new GalleryFolderEntry(path)).ToArray(); GalleryFolderPicker.SelectedValue = folder; }
        finally { syncingFolderPicker = false; }
    }
    private void AddGalleryFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Añadir carpeta a la galería" };
        if (picker.ShowDialog(this) != true) return;
        string folder = Path.GetFullPath(picker.FolderName);
        if (!addedGalleryFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)) addedGalleryFolders.Add(folder);
        try { using var settings = Registry.CurrentUser.CreateSubKey(@"Software\IMGViewer"); settings.SetValue("GalleryFolders", addedGalleryFolders.ToArray(), RegistryValueKind.MultiString); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { StatusText.Text = "La carpeta se añadió para esta sesión, pero no se pudo guardar."; }
        SelectGalleryFolder(folder);
    }
    private void GalleryFolderPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!syncingFolderPicker && GalleryFolderPicker.SelectedValue is string folder) SelectGalleryFolder(folder);
    }
    private void SelectGalleryFolder(string folder)
    {
        selectedGalleryFolder = folder; gallerySearchRoot = null; GallerySearch.Clear(); gallerySignature = null;
        RefreshGallery(true);
    }
    private void GridSelectionCheck_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: GridPhoto photo } && !gridBatchInProgress) SelectGridPhoto(photo, ModifierKeys.Control);
        e.Handled = true;
    }
    private void SelectAllImages_Click(object sender, RoutedEventArgs e) => SelectAllGridImages(gridPhotos.Any(photo => !photo.IsSelected));
    private void SelectAllGridImages(bool select)
    {
        if (gridBatchInProgress) return;
        foreach (var photo in gridPhotos) photo.IsSelected = select;
        UpdateGridSelection();
    }
    private void UpdateSelectAllState()
    {
        if (SelectAllImages is null) return;
        int count = gridPhotos.Count(photo => photo.IsSelected);
        SelectAllImages.IsEnabled = gridPhotos.Length > 0 && !gridBatchInProgress;
        SelectAllImages.IsChecked = count == 0 ? false : count == gridPhotos.Length ? true : null;
    }
    private void UndoWorkspace()
    {
        if (!IsFolderGridOpen && markers.Count > 0) UndoMarker(); else RestorePhoto();
    }
    private void ShowActionButtons()
    {
        var commands = new ShortcutAction[] {
            new("Archivos", "\uE8B7", "open", "Abrir imagen", "Ctrl + O"),
            new("Archivos", "\uE8B7", "folder", "Abrir carpeta", "Ctrl + Shift + O"),
            new("Archivos", "\uE8B7", "save", "Guardar", "Ctrl + S"),
            new("Archivos", "\uE8B7", "savecopy", "Guardar copia / convertir", "Ctrl + Shift + S"),
            new("Archivos", "\uE8B7", "copy", "Copiar", "Ctrl + C"),
            new("Archivos", "\uE8B7", "rename", "Renombrar", "F2"),
            new("Archivos", "\uE8B7", "delete", "Enviar a la Papelera", "Supr"),
            new("Archivos", "\uE8B7", "restore", "Recuperar eliminación", "Shift + Supr"),
            new("Archivos", "\uE8B7", "selectall", "Seleccionar todas", "Ctrl + A"),
            new("Archivos", "\uE8B7", "convert", "Convertir selección", ""),
            new("Navegación y zoom", "\uE71E", "previous", "Imagen anterior", "← / rueda ↑"),
            new("Navegación y zoom", "\uE71E", "next", "Imagen siguiente", "→ / rueda ↓"),
            new("Navegación y zoom", "\uE71E", "zoomin", "Ampliar", "Ctrl + rueda ↑"),
            new("Navegación y zoom", "\uE71E", "zoomout", "Reducir", "Ctrl + rueda ↓"),
            new("Navegación y zoom", "\uE71E", "scale50", "Tamaño 50%", "Alt + 1"),
            new("Navegación y zoom", "\uE71E", "scale100", "Tamaño 100%", "Alt + 2"),
            new("Navegación y zoom", "\uE71E", "scale150", "Tamaño 150%", "Alt + 3"),
            new("Navegación y zoom", "\uE71E", "cover", "Cubrir / ajustar", "Z"),
            new("Navegación y zoom", "\uE71E", "fit", "Restablecer zoom", "Clic central con zoom"),
            new("Navegación y zoom", "\uE71E", "pan", "Desplazar imagen ampliada", "Ctrl + arrastrar / arrastre central"),
            new("Navegación y zoom", "\uE71E", "grid", "Cuadrícula / imagen", "G"),
            new("Navegación y zoom", "\uE71E", "background", "Fondo sólido / cuadrícula", "T"),
            new("Marcación y edición", "\uE70F", "movetool", "Mover ventana", "V / arrastrar"),
            new("Marcación y edición", "\uE70F", "arrowtool", "Dibujar flecha", "Alt + arrastrar"),
            new("Marcación y edición", "\uE70F", "curvedarrowtool", "Dibujar flecha curva", "Ctrl + Shift + arrastrar"),
            new("Marcación y edición", "\uE70F", "rectangletool", "Dibujar rectángulo", "Shift + arrastrar"),
            new("Marcación y edición", "\uE70F", "freehandtool", "Mano alzada", "Alt + Shift + arrastrar"),
            new("Marcación y edición", "\uE70F", "texttool", "Texto", "Ctrl + T"),
            new("Marcación y edición", "\uE70F", "blurtool", "Blur", "Ctrl + B"),
            new("Marcación y edición", "\uE70F", "undo", "Deshacer marca o eliminación", "Ctrl + Z"),
            new("Marcación y edición", "\uE70F", "rotate", "Girar a la derecha", "R"),
            new("Marcación y edición", "\uE70F", "rotateback", "Girar a la izquierda", "Shift + R"),
            new("Marcación y edición", "\uE70F", "discard", "Anular cambios pendientes", "Esc"),
            new("Marcación y edición", "\uE70F", "inlinecrop", "Activar / desactivar recorte", ""),
            new("Marcación y edición", "\uE70F", "crop", "Recortar y enderezar", ""),
            new("Marcación y edición", "\uE70F", "resize", "Redimensionar", ""),
            new("Capturas y ventana", "\uE722", "capture", "Capturar pantalla", captureHotkey.Label),
            new("Capturas y ventana", "\uE722", "fullscreen", "Pantalla completa", "Doble clic"),
            new("Capturas y ventana", "\uE722", "zen", "Modo zen", "Tab"),
            new("Capturas y ventana", "\uE722", "topmost", "Siempre visible", "Ctrl + F12"),
            new("Capturas y ventana", "\uE722", "normalmode", "Salir del modo zen", "Esc"),
            new("Capturas y ventana", "\uE722", "minimize", "Minimizar ventana", "Esc + Esc"),
            new("Capturas y ventana", "\uE722", "close", "Ocultar / cerrar ventana", ""),
            new("Capturas y ventana", "\uE722", "exit", "Salir completamente", "Shift + cerrar"),
            new("Capturas y ventana", "\uE722", "settings", "Configuración", "")
        };
        bool image = Photo.Source is BitmapSource && !IsFolderGridOpen;
        foreach (var command in commands)
            command.Enabled = command.Action switch {
                "selectall" => IsFolderGridOpen && gridPhotos.Length > 0,
                "resize" or "convert" => IsFolderGridOpen ? SelectedGridPaths().Length > 0 && !gridBatchInProgress : image,
                "restore" => recycledPhotos.Count > 0,
                "copy" or "delete" => IsFolderGridOpen ? SelectedGridPaths().Length > 0 && !gridBatchInProgress : image,
                "rename" => currentIndex >= 0,
                "previous" or "next" => currentIndex >= 0 && folderImages.Length > 1,
                "save" or "savecopy" or "rotate" or "rotateback" or "scale50" or "scale100" or "scale150" or "zoomin" or "zoomout" or "cover" or "fit" or "pan" or "arrowtool" or "curvedarrowtool" or "rectangletool" or "freehandtool" or "texttool" or "blurtool" or "movetool" or "inlinecrop" or "crop" => image,
                "normalmode" => zenMode,
                "discard" => HasPendingChanges,
                "undo" => markers.Count > 0 || recycledPhotos.Count > 0,
                _ => true };
        var dialog = new ShortcutsWindow(commands, action => Dispatcher.InvokeAsync(() => {
            Activate(); WorkspaceAction_Click(new Button { Tag = action }, new RoutedEventArgs());
        })) { Owner = this, Width = Math.Min(1040, Math.Max(360, ActualWidth * 0.9)), Height = Math.Min(760, SystemParameters.WorkArea.Height * 0.85) };
        dialog.ShowDialog();
    }
}
