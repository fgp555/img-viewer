using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private Point? galleryDragStart;
    private object? galleryDragItem;
    private ListBox? galleryDragList;
    private bool galleryExternalDrag;
    private static T? GalleryAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match) return match;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }
    private void GalleryDrag_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox list || e.OriginalSource is not DependencyObject source) return;
        if (GalleryAncestor<System.Windows.Controls.Primitives.TextBoxBase>(source) is not null) return;
        StopGalleryScroll();
        if (galleryRenamePhoto is not null && !CommitGalleryRename()) { e.Handled = true; return; }
        var item = GalleryAncestor<ListBoxItem>(source);
        if (item is null) return;
        // Leave independent actions such as removing a capture untouched.
        if (GalleryAncestor<Button>(source) is { } button && button.DataContext is SessionCapture &&
            button.Name == "")
        {
            // Capture card has a large open button; the small remove button has an accessible name.
            if (System.Windows.Automation.AutomationProperties.GetName(button) == "Quitar captura") return;
        }
        if (item.DataContext is not GalleryPhoto && item.DataContext is not SessionCapture) return;
        galleryDragStart = e.GetPosition(this);
        galleryDragItem = item.DataContext;
        galleryDragList = list;
        Keyboard.Focus(list);
        if (!list.CaptureMouse()) { ClearGalleryDrag(); return; }
        e.Handled = true;
    }
    private void GalleryDrag_MouseMove(object sender, MouseEventArgs e)
    {
        if (galleryExternalDrag || galleryDragStart is not { } start || galleryDragItem is not { } item) return;
        if (e.LeftButton != MouseButtonState.Pressed) { ClearGalleryDrag(); return; }
        Point current = e.GetPosition(this);
        if (Math.Abs(current.X-start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y-start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        e.Handled = true;
        ClearGalleryDrag();
        galleryExternalDrag = true;
        try
        {
            string path;
            BitmapSource? preview = null;
            if (item is GalleryPhoto photo)
            {
                path = photo.Path;
                if (!File.Exists(path)) { StatusText.Text = "La imagen ya no está disponible."; return; }
                // Use an already decoded thumbnail when available; exporting the original never re-encodes it.
                var container = GalleryList.ItemContainerGenerator.ContainerFromItem(photo) as ListBoxItem;
                preview = FindGalleryDragImage(container)?.Source as BitmapSource;
            }
            else if (item is SessionCapture capture)
            {
                if (ReferenceEquals(activeCapture, capture)) { EndInlineText(true); RememberActiveCapture(); }
                preview = capture.Preview;
                path = ExportCaptureForDrag(capture.Preview);
            }
            else return;
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { path });
            using var preferred = new MemoryStream(BitConverter.GetBytes((int)DragDropEffects.Copy));
            data.SetData("Preferred DropEffect", preferred);
            using var visual = new GalleryDragPreview(preview, Path.GetFileName(path));
            GiveFeedbackEventHandler feedback = (_,args) => { visual.FollowCursor(); args.UseDefaultCursors = true; };
            GiveFeedback += feedback;
            try { DragDrop.DoDragDrop(this, data, DragDropEffects.Copy); }
            finally { GiveFeedback -= feedback; }
        }
        catch (Exception ex) when (IsBatchImageError(ex) || ex is System.Runtime.InteropServices.ExternalException)
        { StatusText.Text = "No se pudo preparar la imagen para arrastrar."; }
        finally { galleryExternalDrag = false; }
    }
    private static Image? FindGalleryDragImage(DependencyObject? parent)
    {
        if (parent is null) return null;
        for (int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);
            if (child is Image image) return image;
            if (FindGalleryDragImage(child) is { } found) return found;
        }
        return null;
    }
    private void GalleryDrag_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (galleryDragItem is not { } item) return;
        var list = galleryDragList;
        bool inside = list is not null && new Rect(list.RenderSize).Contains(e.GetPosition(list));
        ClearGalleryDrag();
        e.Handled = true;
        if (!inside) return;
        if (item is GalleryPhoto) list!.SelectedItem = item;
        else if (item is SessionCapture capture) ShowSessionCapture(capture);
    }
    private void GalleryDrag_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, galleryDragList)) ClearGalleryDrag();
    }
    private void ClearGalleryDrag()
    {
        var list=galleryDragList;
        galleryDragStart=null; galleryDragItem=null; galleryDragList=null;
        if (list?.IsMouseCaptured == true) list.ReleaseMouseCapture();
    }
    private static string ExportCaptureForDrag(BitmapSource bitmap)
    {
        string folder=Path.Combine(Path.GetTempPath(), "IMGViewer", "DragExports");
        Directory.CreateDirectory(folder);
        // Destinations can read dropped files asynchronously. Retain recent exports.
        foreach (string old in Directory.EnumerateFiles(folder, "screen-*"))
        {
            try { if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddDays(-7)) File.Delete(old); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        string name=Path.GetFileNameWithoutExtension(CapturePreferences.FileName(DateTime.Now))+"-"+Guid.NewGuid().ToString("N")[..8]+CapturePreferences.FormatExtension;
        return WriteUniquePhoto(bitmap,folder,name);
    }
}
