using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace IMGViewer;
public partial class MainWindow
{
    private BitmapSource? inlineCropBitmap;
    private Rect inlineCropBounds;
    private bool inlineCropPending;
    private readonly List<Thumb> inlineCropHandles=[];
    private System.Windows.Shapes.Path? inlineCropMask;
    private Rectangle? inlineCropOutline;
    private void CropButton_Click(object sender,RoutedEventArgs e)
    {
        if(inlineCropBitmap is null) EnableInlineCrop(); else CancelInlineCrop();
    }
    private void EnableInlineCrop()
    {
        if(Photo.Source is not BitmapSource bitmap || IsFolderGridOpen) return;
        SetSlideShowPlaying(false);
        inlineCropBitmap=bitmap;
        inlineCropBounds=new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight);
        inlineCropPending=false;
        if(inlineCropMask is null)
        {
            inlineCropMask=new System.Windows.Shapes.Path { Fill=new SolidColorBrush(Color.FromArgb(145,0,0,0)), IsHitTestVisible=false };
            inlineCropOutline=new Rectangle { Stroke=Brushes.White, StrokeThickness=1, IsHitTestVisible=false };
            InlineCropCanvas.Children.Add(inlineCropMask); InlineCropCanvas.Children.Add(inlineCropOutline);
            foreach(string edge in new[]{"nw","n","ne","e","se","s","sw","w"})
            {
                var handle=new Thumb { Width=10,Height=10,Tag=edge,Cursor=edge is "n" or "s"?Cursors.SizeNS:edge is "w" or "e"?Cursors.SizeWE:edge is "nw" or "se"?Cursors.SizeNWSE:Cursors.SizeNESW, ToolTip="Arrastra para recortar · Se aplica al soltar · Esc cancela el arrastre", Focusable=true };
                var border=new FrameworkElementFactory(typeof(Border));
                border.SetValue(Border.BackgroundProperty,Brushes.White); border.SetValue(Border.BorderBrushProperty,Brushes.Black); border.SetValue(Border.BorderThicknessProperty,new Thickness(1));
                handle.Template=new ControlTemplate(typeof(Thumb)) { VisualTree=border };
                System.Windows.Automation.AutomationProperties.SetName(handle,"Recortar borde "+edge);
                handle.DragStarted+=(_,_)=>{ EndInlineText(true); Keyboard.Focus(this); };
                handle.DragDelta+=InlineCrop_DragDelta;
                handle.DragCompleted+=InlineCrop_DragCompleted;
                InlineCropCanvas.Children.Add(handle); inlineCropHandles.Add(handle);
            }
        }
        UpdateInlineCropAdorners();
    }
    private void InlineCrop_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if(inlineCropBitmap is not { } bitmap) return;
        if(e.Canceled)
        {
            inlineCropBounds = new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight);
            inlineCropPending = false;
            UpdateInlineCropAdorners();
            return;
        }
        if(!inlineCropPending) return;
        ApplyInlineCrop();
        // Keep the grips available for another immediate adjustment.
        if(inlineCropBitmap is null && !ReferenceEquals(Photo.Source,bitmap)) EnableInlineCrop();
    }

    private Rect InlineCropViewBounds(Rect pixels)
    {
        var bitmap=inlineCropBitmap!;
        var photoRect=new Rect(pixels.X/bitmap.PixelWidth*Photo.ActualWidth,pixels.Y/bitmap.PixelHeight*Photo.ActualHeight,
            pixels.Width/bitmap.PixelWidth*Photo.ActualWidth,pixels.Height/bitmap.PixelHeight*Photo.ActualHeight);
        return Photo.TransformToAncestor(PhotoViewport).TransformBounds(photoRect);
    }
    private void InlineCrop_DragDelta(object sender,DragDeltaEventArgs e)
    {
        if(inlineCropBitmap is not { } bitmap || sender is not Thumb { Tag:string edge }) return;
        Rect full=InlineCropViewBounds(new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight));
        if(full.Width<=0 || full.Height<=0) return;
        double dx=e.HorizontalChange*bitmap.PixelWidth/full.Width, dy=e.VerticalChange*bitmap.PixelHeight/full.Height;
        double left=inlineCropBounds.Left,top=inlineCropBounds.Top,right=inlineCropBounds.Right,bottom=inlineCropBounds.Bottom;
        if(edge.Contains('w')) left=Math.Clamp(left+dx,0,right-1);
        if(edge.Contains('e')) right=Math.Clamp(right+dx,left+1,bitmap.PixelWidth);
        if(edge.Contains('n')) top=Math.Clamp(top+dy,0,bottom-1);
        if(edge.Contains('s')) bottom=Math.Clamp(bottom+dy,top+1,bitmap.PixelHeight);
        inlineCropBounds=new Rect(left,top,right-left,bottom-top);
        inlineCropPending=left>0.01 || top>0.01 || right<bitmap.PixelWidth-0.01 || bottom<bitmap.PixelHeight-0.01;
        UpdateInlineCropAdorners(); e.Handled=true;
    }
    private void UpdateInlineCropAdorners()
    {
        if(InlineCropCanvas is null) return;
        if(inlineCropBitmap is not null && !ReferenceEquals(Photo.Source,inlineCropBitmap)) { CancelInlineCrop(); return; }
        bool visible=inlineCropBitmap is not null && !IsFolderGridOpen && Photo.ActualWidth>0 && Photo.ActualHeight>0;
        InlineCropCanvas.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        CropButton.Background = inlineCropBitmap is null ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(52,54,55));

        if(!visible || inlineCropMask is null || inlineCropOutline is null) return;
        Rect rect=InlineCropViewBounds(inlineCropBounds);
        Rect full=InlineCropViewBounds(new Rect(0,0,inlineCropBitmap!.PixelWidth,inlineCropBitmap.PixelHeight));
        inlineCropMask.Data=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(full),new RectangleGeometry(rect));
        Canvas.SetLeft(inlineCropOutline,rect.Left); Canvas.SetTop(inlineCropOutline,rect.Top);
        inlineCropOutline.Width=rect.Width; inlineCropOutline.Height=rect.Height;
        foreach(var handle in inlineCropHandles)
        {
            string edge=(string)handle.Tag;
            double x=edge.Contains('w')?rect.Left:edge.Contains('e')?rect.Right:rect.Left+rect.Width/2;
            double y=edge.Contains('n')?rect.Top:edge.Contains('s')?rect.Bottom:rect.Top+rect.Height/2;
            // Keep the entire grip reachable when the image meets the viewport edge.
            Canvas.SetLeft(handle,Math.Clamp(x-5,0,Math.Max(0,PhotoViewport.ActualWidth-10)));
            Canvas.SetTop(handle,Math.Clamp(y-5,0,Math.Max(0,PhotoViewport.ActualHeight-10)));
        }
    }
    private bool HandleInlineCropKey(KeyEventArgs e)
    {
        if(inlineCropBitmap is null || IsFolderGridOpen || Keyboard.FocusedElement is TextBoxBase or ComboBox || MainMenu.IsKeyboardFocusWithin || Workspace.ContextMenu?.IsOpen==true) return false;
        if(e.Key==Key.Escape) { e.Handled=true; CancelInlineCrop(); return true; }
        if(e.Key==Key.Enter && Keyboard.Modifiers==ModifierKeys.None) { e.Handled=true; ApplyInlineCrop(); return true; }
        return false;
    }

    private void CancelInlineCrop()
    {
        inlineCropBitmap=null; inlineCropPending=false;
        if(CropButton is not null) CropButton.ClearValue(BackgroundProperty);
        foreach(var handle in inlineCropHandles) if(handle.IsMouseCaptured) handle.ReleaseMouseCapture();
        if(InlineCropCanvas is not null) InlineCropCanvas.Visibility=Visibility.Collapsed;

    }
    private void ApplyInlineCrop()
    {
        if(inlineCropBitmap is not { } bitmap) return;
        if(!inlineCropPending) { CancelInlineCrop(); return; }
        EndInlineText(true);
        int left=Math.Clamp((int)Math.Floor(inlineCropBounds.Left),0,bitmap.PixelWidth-1);
        int top=Math.Clamp((int)Math.Floor(inlineCropBounds.Top),0,bitmap.PixelHeight-1);
        int right=Math.Clamp((int)Math.Ceiling(inlineCropBounds.Right),left+1,bitmap.PixelWidth);
        int bottom=Math.Clamp((int)Math.Ceiling(inlineCropBounds.Bottom),top+1,bitmap.PixelHeight);
        try
        {
            var result=new CroppedBitmap(FlattenMarkers(bitmap),new Int32Rect(left,top,right-left,bottom-top)); result.Freeze();
            CancelInlineCrop(); ApplyPixelEdit(result); RememberActiveCapture();
            StatusText.Text="Recorte aplicado · Ctrl + C copia · Ctrl + S guarda.";
        }
        catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or System.Runtime.InteropServices.ExternalException)
        { StatusText.Text="No se pudo aplicar el recorte."; }
    }
}
