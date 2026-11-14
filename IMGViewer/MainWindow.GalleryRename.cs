using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace IMGViewer;
public partial class MainWindow
{
    private GalleryPhoto? galleryRenamePhoto;
    private TextBox? galleryRenameEditor;
    private bool finishingGalleryRename;
    private bool HandleGalleryInteractionKey(KeyEventArgs e)
    {
        if(galleryRenamePhoto is not null && galleryRenameEditor?.IsKeyboardFocusWithin == true)
        {
            if(e.Key==Key.Escape) { e.Handled=true; EndGalleryRename(); Keyboard.Focus(GalleryList); return true; }
            if(e.Key==Key.Enter || e.Key is Key.Up or Key.Down && Keyboard.Modifiers==ModifierKeys.None)
            {
                e.Handled=true;
                var photos=GalleryList.Items.Cast<GalleryPhoto>().ToArray();
                int index=Array.IndexOf(photos,galleryRenamePhoto);
                int next=index+(e.Key==Key.Up?-1:e.Key==Key.Down?1:0);
                if(!CommitGalleryRename()) return true;
                if(e.Key is Key.Up or Key.Down && next>=0 && next<photos.Length)
                {
                    GalleryList.SelectedItem=photos[next];
                    BeginGalleryRename(photos[next]);
                }
                else Keyboard.Focus(GalleryList);
                return true;
            }
            return false;
        }
        if(GalleryList.IsVisible && GalleryList.IsKeyboardFocusWithin && Keyboard.Modifiers==ModifierKeys.None)
        {
            if(e.Key==Key.F2) { e.Handled=true; if(!e.IsRepeat) TryBeginGalleryRename(); return true; }
            if(e.Key is Key.Up or Key.Down)
            {
                e.Handled=true;
                int index=GalleryList.SelectedIndex;
                int next=Math.Clamp(index+(e.Key==Key.Down?1:-1),0,Math.Max(0,GalleryList.Items.Count-1));
                if(GalleryList.Items.Count>0) { GalleryList.SelectedIndex=next; Keyboard.Focus(GalleryList); }
                return true;
            }
        }
        return false;
    }
    private bool TryBeginGalleryRename()
    {
        if(!GalleryList.IsVisible || historyView || GalleryList.SelectedItem is not GalleryPhoto photo) return false;
        SetSlideShowPlaying(false); BeginGalleryRename(photo); return true;
    }
    private void BeginGalleryRename(GalleryPhoto photo)
    {
        EndGalleryRename(); StopGalleryScroll();
        galleryRenamePhoto=photo;
        GalleryList.ScrollIntoView(photo);
        Dispatcher.InvokeAsync(() => {
            if(!ReferenceEquals(galleryRenamePhoto,photo)) return;
            GalleryList.UpdateLayout();
            var tile=GalleryList.ItemContainerGenerator.ContainerFromItem(photo) as ListBoxItem;
            var editor=GalleryChild<TextBox>(tile,"GalleryRenameEditor");
            if(editor is null) { EndGalleryRename(); return; }
            galleryRenameEditor=editor; editor.Text=photo.DisplayName;
            editor.Visibility=Visibility.Visible;
            editor.Focus(); editor.SelectAll();
        },DispatcherPriority.Loaded);
    }
    private void ShowGalleryRenameError(string message)
    {
        var tile=GalleryList.ItemContainerGenerator.ContainerFromItem(galleryRenamePhoto) as ListBoxItem;
        if(GalleryChild<TextBlock>(tile,"GalleryRenameErrorText") is { } text) text.Text=message;
        if(GalleryChild<Border>(tile,"GalleryRenameError") is { } border) border.Visibility=Visibility.Visible;
        if(galleryRenameEditor is { } editor) editor.ToolTip=message;
        StatusText.Text=message;
    }
    private bool CommitGalleryRename()
    {
        if(finishingGalleryRename || galleryRenamePhoto is not { } photo || galleryRenameEditor is not { } editor) return true;
        string name=editor.Text.Trim();
        string stem=name.Split('.')[0].ToUpperInvariant();
        bool reserved=stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length==4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9');
        if(name.Length==0 || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || reserved)
        { ShowGalleryRenameError("Escribe un nombre válido, sin la extensión."); return false; }
        string old=photo.Path;
        string destination=Path.Combine(Path.GetDirectoryName(old)!,name+Path.GetExtension(old));
        if(StringComparer.Ordinal.Equals(old,destination)) { EndGalleryRename(); return true; }
        try
        {
            File.Move(old,destination);
            finishingGalleryRename=true;
            for(int i=0;i<folderImages.Length;i++) if(StringComparer.OrdinalIgnoreCase.Equals(folderImages[i],old)) folderImages[i]=destination;
            foreach(var capture in captureHistory) if(StringComparer.OrdinalIgnoreCase.Equals(capture.FilePath,old)) capture.FilePath=destination;
            if(StringComparer.OrdinalIgnoreCase.Equals(captureExportPath,old)) captureExportPath=destination;
            photo.Path=destination;
            if(StringComparer.OrdinalIgnoreCase.Equals(CurrentGridImage,destination))
            { Title=$"IMG Viewer — {Path.GetFileName(destination)}"; ShowPosition(); UpdateSlideShowAvailability(); }
            // Keep the current collection order and item identity while renaming a sequence.
            StatusText.Text="Archivo renombrado.";
            EndGalleryRename();
            if(IsFolderGridOpen) LoadFolderGrid(GridFolderPath.Text);
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
        {
            ShowGalleryRenameError(!StringComparer.OrdinalIgnoreCase.Equals(old,destination) && Path.Exists(destination)
                ? "Ya existe un archivo o carpeta con ese nombre." : "No se pudo renombrar el archivo.");
            return false;
        }
        finally { finishingGalleryRename=false; }
    }
    private void EndGalleryRename()
    {
        finishingGalleryRename=true;
        var photo=galleryRenamePhoto; var editor=galleryRenameEditor;
        galleryRenamePhoto=null; galleryRenameEditor=null;
        if(editor is not null) { editor.Visibility=Visibility.Collapsed; editor.ToolTip=null; }
        var tile=photo is null?null:GalleryList.ItemContainerGenerator.ContainerFromItem(photo) as ListBoxItem;
        if(GalleryChild<Border>(tile,"GalleryRenameError") is { } error) error.Visibility=Visibility.Collapsed;
        finishingGalleryRename=false;
    }
    private void GalleryRename_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if(finishingGalleryRename || !ReferenceEquals(sender,galleryRenameEditor)) return;
        if(!CommitGalleryRename() && IsActive)
            Dispatcher.InvokeAsync(() => galleryRenameEditor?.Focus(),DispatcherPriority.Input);
    }
}
