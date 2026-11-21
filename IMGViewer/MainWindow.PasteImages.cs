using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private readonly Dictionary<int,Thumb> pastedImageBodies=[];
    private readonly Dictionary<string,Thumb> pastedImageCorners=[];
    private Border? pastedImageFrame;
    private System.Windows.Shapes.Line? pastedGuideX,pastedGuideY;
    private int selectedPastedImage=-1;
    private BitmapSource? pastedAdornerBase;
    private (int Index,StyledMarker Before,Thumb Thumb)? pastedImageGesture;
    private sealed record PastedTransformUndo(int Index,int Count,StyledMarker Before,StyledMarker After, BitmapSource? CanvasBefore=null, StyledMarker[]? MarksBefore=null, bool PixelEdit=false);
    private readonly Stack<PastedTransformUndo> pastedTransformUndo=[];
    private bool CanUndoPastedTransform=>pastedTransformUndo.TryPeek(out var state)&&markers.Count==state.Count&&state.Index<markers.Count&&ReferenceEquals(markers[state.Index],state.After);
    private sealed record PastedDeleteUndo(BitmapSource Photo,int Index,StyledMarker Removed,StyledMarker[] Remaining);
    private readonly Stack<PastedDeleteUndo> pastedDeleteUndo=[];
    private bool CanUndoPastedDelete=>pastedDeleteUndo.TryPeek(out var state)&&ReferenceEquals(Photo.Source,state.Photo)&&
        markers.Count==state.Remaining.Length&&markers.Select((mark,index)=>ReferenceEquals(mark,state.Remaining[index])).All(same=>same);
    private bool DeleteSelectedPastedImage()
    {
        int index=selectedPastedImage;
        if(IsFolderGridOpen||pastedImageGesture is not null||index<0||index>=markers.Count||!markers[index].IsPastedImage||Photo.Source is not BitmapSource bitmap)return false;
        var removed=markers[index];markers.RemoveAt(index);
        if(pastedDeleteUndo.Count>=100)pastedDeleteUndo.Clear();
        pastedDeleteUndo.Push(new(bitmap,index,removed,markers.ToArray()));selectedPastedImage=-1;
        RefreshMarkerOverlay();UpdatePendingSaveIndicator();
        StatusText.Text="Imagen pegada eliminada · Ctrl + Z para recuperarla.";return true;
    }
    private void UndoPastedDelete()
    {
        if(!CanUndoPastedDelete)return;
        var state=pastedDeleteUndo.Pop();markers.Insert(state.Index,state.Removed);selectedPastedImage=state.Index;
        RefreshMarkerOverlay();UpdatePendingSaveIndicator();
        StatusText.Text="Imagen pegada recuperada.";
    }
    private void PasteClipboardImage()
    {
        if(IsFolderGridOpen||Mouse.Captured is not null)return;
        try
        {
            var image=Clipboard.GetImage();
            if(image is null){StatusText.Text="El portapapeles no contiene una imagen.";return;}
            AddPastedImage(FrozenDraftBitmap(image));
        }
        catch(Exception ex) when(ex is System.Runtime.InteropServices.ExternalException or InvalidOperationException or NotSupportedException)
        {StatusText.Text="No se pudo pegar la imagen. Inténtalo de nuevo.";}
    }
    private void AddPastedImage(BitmapSource image)
    {
        EndInlineText(true);CancelInlineCrop();SetSlideShowPlaying(false);
        if(Photo.Source is not BitmapSource bitmap)
        {
            RememberActiveCapture();activeCapture=null;
            OpenCapture(image);AddCaptureToHistory(DateTime.Now);
            SelectWindowMoveTool();
            StatusText.Text="Imagen pegada como base · Ctrl + V añade otra imagen.";
            return;
        }
        double scale=Math.Min(1,Math.Min(bitmap.PixelWidth*0.65/image.PixelWidth,bitmap.PixelHeight*0.65/image.PixelHeight));
        double width=image.PixelWidth*scale*bitmap.Width/bitmap.PixelWidth,height=image.PixelHeight*scale*bitmap.Height/bitmap.PixelHeight;
        var bounds=new Rect((bitmap.Width-width)/2,(bitmap.Height-height)/2,width,height);
        var geometry=new RectangleGeometry(bounds);geometry.Freeze();
        markers.Add(new StyledMarker(geometry,Brushes.Transparent,1,false,Patch:image,PatchBounds:bounds,IsPastedImage:true));
        pastedAdornerBase=bitmap;selectedPastedImage=markers.Count-1;
        SelectWindowMoveTool();RefreshMarkerOverlay();UpdatePendingSaveIndicator();
        StatusText.Text="Imagen pegada · Arrastra para mover · Esquinas para redimensionar · Ctrl + Z deshace.";
    }
    private bool HandlePastedImageDragKey(KeyEventArgs e)
    {
        if(pastedImageGesture is not { } gesture)return false;
        if(e.Key==Key.Escape)gesture.Thumb.CancelDrag();
        e.Handled=true;return true;
    }
    private bool IsPastedImageThumb(DependencyObject? source)=>GalleryAncestor<Thumb>(source) is { } thumb && PastedImagesCanvas.Children.Contains(thumb);
    private void UpdatePastedHitTesting()
    {
        if(PastedImagesCanvas is not null && pastedImageGesture is null)
            PastedImagesCanvas.IsHitTestVisible=Keyboard.Modifiers==ModifierKeys.None && activeMarkerTool is null && !panTool;
    }
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);UpdatePastedHitTesting();
    }
    private void UpdatePastedImageAdorners()
    {
        if(PastedImagesCanvas is null)return;
        if(!ReferenceEquals(pastedAdornerBase,Photo.Source)){pastedAdornerBase=Photo.Source as BitmapSource;selectedPastedImage=-1;pastedTransformUndo.Clear();}
        int[] indices=markers.Select((mark,index)=>(mark,index)).Where(x=>x.mark.IsPastedImage&&x.mark.Patch is not null).Select(x=>x.index).ToArray();
        bool visible=Photo.Source is BitmapSource && indices.Length>0 && !IsFolderGridOpen && !zenMode && !windowedMode.HasValue && Photo.ActualWidth>0 && Photo.ActualHeight>0;
        PastedImagesCanvas.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        if(!visible)return;
        if(!pastedImageBodies.Keys.SequenceEqual(indices))
        {
            PastedImagesCanvas.Children.Clear();pastedImageBodies.Clear();pastedImageCorners.Clear();
            foreach(int index in indices)
            {
                int imageIndex=index;var body=NewPastedThumb(Cursors.SizeAll,"Mover imagen pegada");
                body.DragStarted+=(_,_)=>BeginPastedImageGesture(imageIndex,body);
                body.DragDelta+=(_,args)=>ChangePastedImage(imageIndex,null,args.HorizontalChange,args.VerticalChange);
                body.DragCompleted+=(_,args)=>CompletePastedImageGesture(args.Canceled);
                AddPastedPlacementMenu(body,imageIndex);
                pastedImageBodies.Add(index,body);PastedImagesCanvas.Children.Add(body);
            }
            pastedGuideX=new System.Windows.Shapes.Line {Stroke=Brushes.DeepSkyBlue,StrokeThickness=1,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
            pastedGuideY=new System.Windows.Shapes.Line {Stroke=Brushes.DeepSkyBlue,StrokeThickness=1,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
            PastedImagesCanvas.Children.Add(pastedGuideX);PastedImagesCanvas.Children.Add(pastedGuideY);
            pastedImageFrame=new Border {BorderBrush=new SolidColorBrush(Color.FromRgb(66,165,255)),BorderThickness=new Thickness(1),IsHitTestVisible=false};PastedImagesCanvas.Children.Add(pastedImageFrame);
            foreach(string corner in new[]{"nw","ne","se","sw"})
            {
                string edge=corner;var handle=NewPastedThumb(edge is "nw" or "se"?Cursors.SizeNWSE:Cursors.SizeNESW,"Redimensionar imagen pegada");handle.Width=handle.Height=10;
                var visual=new FrameworkElementFactory(typeof(Border));visual.SetValue(Border.BackgroundProperty,Brushes.White);visual.SetValue(Border.BorderBrushProperty,Brushes.Black);visual.SetValue(Border.BorderThicknessProperty,new Thickness(1));handle.Template=new ControlTemplate(typeof(Thumb)){VisualTree=visual};
                handle.DragStarted+=(_,_)=>BeginPastedImageGesture(selectedPastedImage,handle);
                handle.DragDelta+=(_,args)=>ChangePastedImage(selectedPastedImage,edge,args.HorizontalChange,args.VerticalChange);
                handle.DragCompleted+=(_,args)=>CompletePastedImageGesture(args.Canceled);
                pastedImageCorners.Add(corner,handle);PastedImagesCanvas.Children.Add(handle);
            }
        }
        if(!indices.Contains(selectedPastedImage)||activeMarkerTool is not null||panTool)selectedPastedImage=-1;
        foreach(var entry in pastedImageBodies)PositionPastedControl(entry.Value,PastedImageViewBounds(markers[entry.Key].PatchBounds));
        bool selected=selectedPastedImage>=0;
        pastedImageFrame!.Visibility=selected?Visibility.Visible:Visibility.Collapsed;
        foreach(var handle in pastedImageCorners.Values)handle.Visibility=selected?Visibility.Visible:Visibility.Collapsed;
        if(selected)
        {
            var bounds=PastedImageViewBounds(markers[selectedPastedImage].PatchBounds);PositionPastedControl(pastedImageFrame,bounds);
            foreach(var entry in pastedImageCorners)
            {
                double x=entry.Key.Contains('w')?bounds.Left:bounds.Right,y=entry.Key.Contains('n')?bounds.Top:bounds.Bottom;
                Canvas.SetLeft(entry.Value,Math.Clamp(x-5,0,Math.Max(0,PhotoViewport.ActualWidth-10)));Canvas.SetTop(entry.Value,Math.Clamp(y-5,0,Math.Max(0,PhotoViewport.ActualHeight-10)));
            }
        }
        UpdatePastedGuides();UpdatePastedHitTesting();
    }
    private static Thumb NewPastedThumb(Cursor cursor,string label)
    {
        var body=new Thumb {Cursor=cursor,ToolTip=label,Focusable=true};
        var visual=new FrameworkElementFactory(typeof(Border));visual.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") {RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});body.Background=Brushes.Transparent;body.Template=new ControlTemplate(typeof(Thumb)){VisualTree=visual};
        System.Windows.Automation.AutomationProperties.SetName(body,label);return body;
    }
    private static void PositionPastedControl(FrameworkElement control,Rect rect)
    {
        Canvas.SetLeft(control,rect.Left);Canvas.SetTop(control,rect.Top);control.Width=Math.Max(0,rect.Width);control.Height=Math.Max(0,rect.Height);
    }
    private Rect PastedImageViewBounds(Rect bounds)
    {
        var bitmap=(BitmapSource)Photo.Source;
        return Photo.TransformToAncestor(PhotoViewport).TransformBounds(new Rect(bounds.X/bitmap.Width*Photo.ActualWidth,bounds.Y/bitmap.Height*Photo.ActualHeight,bounds.Width/bitmap.Width*Photo.ActualWidth,bounds.Height/bitmap.Height*Photo.ActualHeight));
    }
    private void BeginPastedImageGesture(int index,Thumb thumb)
    {
        if(index<0||index>=markers.Count)return;
        selectedPastedImage=index;pastedImageBodies[index].Background=new ImageBrush(markers[index].Patch) {Stretch=Stretch.Fill};pastedImageGesture=(index,markers[index],thumb);Keyboard.Focus(this);UpdatePastedImageAdorners();
    }
    private void ChangePastedImage(int index,string? corner,double horizontal,double vertical)
    {
        if(index<0||index>=markers.Count||Photo.Source is not BitmapSource bitmap)return;
        var view=PastedImageViewBounds(new Rect(0,0,bitmap.Width,bitmap.Height));if(view.Width<=0||view.Height<=0)return;
        double dx=horizontal*bitmap.Width/view.Width,dy=vertical*bitmap.Height/view.Height;
        var mark=markers[index];Rect bounds=mark.PatchBounds;
        if(corner is null)
        {
            double x=bounds.X+dx,y=bounds.Y+dy;
            double toleranceX=5*bitmap.Width/view.Width,toleranceY=5*bitmap.Height/view.Height;
            foreach(double target in new[]{-bounds.Width,0.0,bitmap.Width-bounds.Width,bitmap.Width})
                if(Math.Abs(x-target)<toleranceX){x=target;break;}
            foreach(double target in new[]{-bounds.Height,0.0,bitmap.Height-bounds.Height,bitmap.Height})
                if(Math.Abs(y-target)<toleranceY){y=target;break;}
            bounds=new Rect(x,y,bounds.Width,bounds.Height);
        }
        else
        {
            bool west=corner.Contains('w'),north=corner.Contains('n');double ratio=bounds.Width/bounds.Height;
            double anchorX=west?bounds.Right:bounds.Left,anchorY=north?bounds.Bottom:bounds.Top;
            double changeX=west?-dx:dx,changeY=(north?-dy:dy)*ratio;
            double change=Math.Abs(changeX)>=Math.Abs(changeY)?changeX:changeY;
            double maxWidth=16384*bitmap.Width/bitmap.PixelWidth;
            double width=Math.Clamp(bounds.Width+change,Math.Min(8*bitmap.Width/bitmap.PixelWidth,maxWidth),maxWidth),height=width/ratio;
            bounds=new Rect(west?anchorX-width:anchorX,north?anchorY-height:anchorY,width,height);
        }
        var geometry=new RectangleGeometry(bounds);geometry.Freeze();markers[index]=mark with {Geometry=geometry,PatchBounds=bounds};RefreshMarkerOverlay();
    }
    private void CompletePastedImageGesture(bool canceled)
    {
        if(pastedImageGesture is not { } gesture)return;pastedImageGesture=null;
        if(gesture.Index>=markers.Count)return;
        pastedImageBodies[gesture.Index].Background=Brushes.Transparent;
        var after=markers[gesture.Index];
        if(canceled)markers[gesture.Index]=gesture.Before;
        else if(after.PatchBounds!=gesture.Before.PatchBounds)
        {
            if(pastedTransformUndo.Count>=100)pastedTransformUndo.Clear();
            CommitPastedPlacement(gesture.Index,gesture.Before);
        }
        RefreshMarkerOverlay();UpdatePendingSaveIndicator();
    }
    private void UndoPastedTransform()
    {
        if(!CanUndoPastedTransform)return;
        var state=pastedTransformUndo.Pop();
        if(state.CanvasBefore is not null && state.MarksBefore is not null)
        {
            Photo.Source=state.CanvasBefore;pastedAdornerBase=state.CanvasBefore;
            markers.Clear();markers.AddRange(state.MarksBefore);hasPixelEdit=state.PixelEdit;
            ResetPhotoView();UpdateMetadataFooter();
        }
        else markers[state.Index]=state.Before;
        selectedPastedImage=state.Index;
        RefreshMarkerOverlay();UpdatePendingSaveIndicator();
    }
    private void UpdatePastedGuides()
    {
        if(pastedGuideX is null||pastedGuideY is null)return;
        pastedGuideX.Visibility=pastedGuideY.Visibility=Visibility.Collapsed;
        if(pastedImageGesture is not {} gesture||Photo.Source is not BitmapSource bitmap)return;
        var bounds=markers[gesture.Index].PatchBounds;var full=PastedImageViewBounds(new Rect(0,0,bitmap.Width,bitmap.Height));
        foreach(double edge in new[]{0.0,bitmap.Width})
            if(Math.Abs(bounds.Left-edge)<0.001||Math.Abs(bounds.Right-edge)<0.001)
            {
                pastedGuideX.X1=pastedGuideX.X2=edge==0?full.Left:full.Right;pastedGuideX.Y1=0;pastedGuideX.Y2=PhotoViewport.ActualHeight;pastedGuideX.Visibility=Visibility.Visible;break;
            }
        foreach(double edge in new[]{0.0,bitmap.Height})
            if(Math.Abs(bounds.Top-edge)<0.001||Math.Abs(bounds.Bottom-edge)<0.001)
            {
                pastedGuideY.Y1=pastedGuideY.Y2=edge==0?full.Top:full.Bottom;pastedGuideY.X1=0;pastedGuideY.X2=PhotoViewport.ActualWidth;pastedGuideY.Visibility=Visibility.Visible;break;
            }
    }
    private void AddPastedPlacementMenu(Thumb body,int index)
    {
        var menu=new ContextMenu();
        foreach(var pair in new[]{("Colocar a la derecha","right"),("Colocar a la izquierda","left"),("Colocar arriba","top"),("Colocar abajo","bottom")})
        {
            var item=new MenuItem {Header=pair.Item1};string side=pair.Item2;
            item.Click+=(_,_)=>PlacePastedImage(index,side);menu.Items.Add(item);
        }
        body.ContextMenu=menu;
    }
    private void PlacePastedImage(int index,string side)
    {
        if(Photo.Source is not BitmapSource bitmap||index<0||index>=markers.Count)return;
        CancelInlineCrop();var before=markers[index];var bounds=before.PatchBounds;
        bounds.Location=side switch {
            "left"=>new Point(-bounds.Width,0),"top"=>new Point(0,-bounds.Height),
            "bottom"=>new Point(0,bitmap.Height),_=>new Point(bitmap.Width,0)};
        var geometry=new RectangleGeometry(bounds);geometry.Freeze();
        markers[index]=before with {Geometry=geometry,PatchBounds=bounds};selectedPastedImage=index;
        CommitPastedPlacement(index,before);RefreshMarkerOverlay();UpdatePendingSaveIndicator();
    }
    private void CommitPastedPlacement(int index,StyledMarker before)
    {
        var bitmap=(BitmapSource)Photo.Source;
        Rect union=new Rect(0,0,bitmap.Width,bitmap.Height);
        foreach(var mark in markers.Where(m=>m.IsPastedImage))union.Union(mark.PatchBounds);
        if(union.Left>=-0.0001 && union.Top>=-0.0001 && union.Right<=bitmap.Width+0.0001 && union.Bottom<=bitmap.Height+0.0001)
        {pastedTransformUndo.Push(new(index,markers.Count,before,markers[index]));return;}
        double sx=bitmap.PixelWidth/bitmap.Width,sy=bitmap.PixelHeight/bitmap.Height;
        double left=Math.Floor(union.Left*sx+0.000001)/sx,top=Math.Floor(union.Top*sy+0.000001)/sy;
        int width=(int)Math.Ceiling((union.Right-left)*sx-0.000001),height=(int)Math.Ceiling((union.Bottom-top)*sy-0.000001);
        if(width>16384||height>16384||(long)width*height>64000000)
        {
            markers[index]=before;StatusText.Text="El lienzo supera el límite de 64 megapíxeles o 16384 píxeles por lado.";return;
        }
        if(left==0&&top==0&&width==bitmap.PixelWidth&&height==bitmap.PixelHeight)
        {pastedTransformUndo.Push(new(index,markers.Count,before,markers[index]));return;}
        var previous=markers.ToArray();previous[index]=before;bool oldEdit=hasPixelEdit;
        var visual=new DrawingVisual();
        using(var drawing=visual.RenderOpen())drawing.DrawImage(bitmap,new Rect(-left,-top,bitmap.Width,bitmap.Height));
        var expanded=new RenderTargetBitmap(width,height,bitmap.DpiX,bitmap.DpiY,PixelFormats.Pbgra32);expanded.Render(visual);expanded.Freeze();
        var offset=new Vector(-left,-top);
        for(int i=0;i<markers.Count;i++)
        {
            var mark=markers[i];var geometry=PathGeometry.CreateFromGeometry(mark.Geometry).Clone();
            var transforms=new TransformGroup();transforms.Children.Add(geometry.Transform);transforms.Children.Add(new TranslateTransform(offset.X,offset.Y));geometry.Transform=transforms;geometry.Freeze();
            var bounds=mark.PatchBounds;if(mark.Patch is not null)bounds.Offset(offset);
            var curve=mark.Curve is {} c?new ArrowShape(c.Start+offset,c.End+offset,c.Control+offset):null;
            markers[i]=mark with {Geometry=geometry,PatchBounds=bounds,Curve=curve};
        }
        Photo.Source=expanded;pastedAdornerBase=expanded;hasPixelEdit=true;
        pastedTransformUndo.Push(new(index,markers.Count,before,markers[index],bitmap,previous,oldEdit));
        ResetPhotoView();UpdateMetadataFooter();
        StatusText.Text="Lienzo ampliado · Ctrl + Z deshace la colocación y el tamaño.";
    }
}
