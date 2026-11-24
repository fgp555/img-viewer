using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace IMGViewer;
public partial class MainWindow
{
    private sealed record ElementDefinition(MarkerKind Kind,Rect Bounds,Point Start=default,Point End=default,Point Control=default,
        ArrowForm Form=ArrowForm.Classic,string? Text=null,double FontSize=32,double? TextWidth=null,
        Point[]? Points=null,double[]? Pressure=null,bool Brush=false,Point Offset=default,double ScaleX=1,double ScaleY=1);
    private sealed record ElementEditUndo(StyledMarker[] Before,StyledMarker[] After,string Key,long Tick);
    private readonly Stack<ElementEditUndo> elementEdits=[];
    private bool syncingElementProperties;
    private bool SameMarks(StyledMarker[] values)=>values.Length==markers.Count&&values.Select((mark,index)=>ReferenceEquals(mark,markers[index])).All(same=>same);
    private bool CanUndoElementEdit=>elementEdits.TryPeek(out var edit)&&SameMarks(edit.After);
    private static Point ElementPoint(ElementDefinition def,Point point)=>new(point.X*def.ScaleX+def.Offset.X,point.Y*def.ScaleY+def.Offset.Y);
    private static ElementDefinition TransformDefinition(ElementDefinition def,double sx,double sy,double tx,double ty)=>def with
        {ScaleX=def.ScaleX*sx,ScaleY=def.ScaleY*sy,Offset=new Point(def.Offset.X*sx+tx,def.Offset.Y*sy+ty)};
    private static Point ConstrainCircle(Point start,Point end)
    {
        double side=Math.Min(Math.Abs(end.X-start.X),Math.Abs(end.Y-start.Y));
        return new Point(start.X+Math.Sign(end.X-start.X)*side,start.Y+Math.Sign(end.Y-start.Y)*side);
    }
    private static Geometry CreateEllipseMark(Rect bounds,double width)
    {
        var geometry=new EllipseGeometry(bounds).GetWidenedPathGeometry(new Pen(Brushes.Black,width));geometry.Freeze();return geometry;
    }
    private ElementDefinition? CurrentElementDefinition(Geometry geometry)
    {
        if(markerDrag is not {} drag)return null;
        return new ElementDefinition(drag.Kind,new Rect(drag.Start,markerEnd),drag.Start,markerEnd,
            DefaultCurveControl(drag.Start,markerEnd,SelectedArrowForm==ArrowForm.Curved),SelectedArrowForm,
            Points:drag.Kind==MarkerKind.Freehand?freehandPoints.ToArray():null,
            Pressure:drag.Kind==MarkerKind.Freehand?freehandPressure.ToArray():null,Brush:freehandBrush);
    }
    private Geometry BuildElementGeometry(ElementDefinition def,double width)
    {
        Geometry geometry;
        var a=ElementPoint(def,def.Start);var b=ElementPoint(def,def.End);
        var bounds=new Rect(ElementPoint(def,def.Bounds.TopLeft),ElementPoint(def,def.Bounds.BottomRight));
        switch(def.Kind)
        {
            case MarkerKind.Rectangle: geometry=CreateRectangleMark(bounds,width);break;
            case MarkerKind.Ellipse: geometry=CreateEllipseMark(bounds,width);break;
            case MarkerKind.Arrow:
                geometry=def.Form==ArrowForm.Classic?CreateArrowMark(a,b,width):CreateTaperedArrow(a,b,ElementPoint(def,def.Control),width);break;
            case MarkerKind.Text:
                var text=new FormattedText(def.Text??"",CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),def.FontSize*def.ScaleY,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
                if(def.TextWidth is {} wrap)text.MaxTextWidth=Math.Max(1,wrap*def.ScaleX);
                geometry=text.BuildGeometry(bounds.TopLeft);break;
            case MarkerKind.Freehand:
                var points=(def.Points??[]).Select(point=>ElementPoint(def,point)).ToArray();
                geometry=def.Brush?CreateBrushFreehand(points,def.Pressure??points.Select(_=>.7).ToArray(),width):CreateSmoothFreehand(points,points.LastOrDefault(),width);break;
            default:geometry=Geometry.Empty;break;
        }
        if(!geometry.IsFrozen)geometry.Freeze();return geometry;
    }
    private StyledMarker RebuildElement(StyledMarker mark)
    {
        if(mark.Definition is not {} def)return mark;
        var geometry=BuildElementGeometry(def,mark.Width);
        ArrowShape? curve=def.Kind==MarkerKind.Arrow&&def.Form==ArrowForm.Curved
            ?new(ElementPoint(def,def.Start),ElementPoint(def,def.End),ElementPoint(def,def.Control)):null;
        Brush brush=mark.Color;
        if(brush is LinearGradientBrush gradient&&def.Kind==MarkerKind.Arrow&&geometry.Bounds.Width>0&&geometry.Bounds.Height>0)
        {
            var directed=gradient.Clone();var a=ElementPoint(def,def.Start);var b=ElementPoint(def,def.End);var bounds=geometry.Bounds;
            directed.StartPoint=new Point((a.X-bounds.X)/bounds.Width,(a.Y-bounds.Y)/bounds.Height);
            directed.EndPoint=new Point((b.X-bounds.X)/bounds.Width,(b.Y-bounds.Y)/bounds.Height);directed.Freeze();brush=directed;
        }
        return mark with {Geometry=geometry,Curve=curve,Color=brush};
    }
    private void RecordElementEdit(StyledMarker[] before,string key)
    {
        var after=markers.ToArray();long now=Environment.TickCount64;
        if(elementEdits.TryPeek(out var previous)&&previous.Key==key&&now-previous.Tick<500&&before.Length==previous.After.Length&&before.Select((mark,index)=>ReferenceEquals(mark,previous.After[index])).All(same=>same))
        {elementEdits.Pop();before=previous.Before;}
        if(elementEdits.Count>=100)elementEdits.Clear();
        elementEdits.Push(new(before,after,key,now));
    }
    private void UndoElementEdit()
    {
        if(!CanUndoElementEdit)return;var edit=elementEdits.Pop();markers.Clear();markers.AddRange(edit.Before);
        RefreshMarkerOverlay();SyncSelectedElementProperties();UpdatePendingSaveIndicator();
    }
    private void EditSelectedElement(string property,double value=0)
    {
        if(syncingElementProperties||!HasSelectedEditableMarker||pastedImageGesture is not null)return;
        int index=selectedPastedImage;var mark=markers[index];if(mark.IsPastedImage)return;
        var before=markers.ToArray();var def=mark.Definition;
        switch(property)
        {
            case "color":mark=mark with {Color=markerBrush};break;
            case "width":
                if(def is null)return;
                var bitmap=(System.Windows.Media.Imaging.BitmapSource)Photo.Source;
                mark=mark with {Width=Math.Max(1,Math.Max(1,value*.16)*Math.Min(bitmap.Width,bitmap.Height)/700)};break;
            case "shadow":mark=mark with {ShadowIntensity=value/100};break;
            case "contrast":mark=mark with {Contrast=value!=0};break;
            case "fontsize":if(def?.Kind!=MarkerKind.Text)return;mark=mark with {Definition=def with {FontSize=value/def.ScaleY}};break;
            case "arrowform":if(def?.Kind!=MarkerKind.Arrow)return;mark=mark with {Definition=def with {Form=(ArrowForm)(int)value}};break;
            default:return;
        }
        markers[index]=RebuildElement(mark);RecordElementEdit(before,property+":"+index);
        RefreshMarkerOverlay();UpdatePendingSaveIndicator();
    }
    private void SyncSelectedElementProperties()
    {
        if(MarkerProperties is null||syncingElementProperties)return;
        syncingElementProperties=true;syncingMarkerWidth=true;
        try
        {
            foreach(var panel in new[]{MarkerProperties,ArrowProperties,FreehandProperties,TextProperties,BlurProperties,SelectedShadowProperties})panel.ClearValue(VisibilityProperty);
            MarkerWidthSlider.IsEnabled=true;
            if(!HasSelectedEditableMarker)return;
            var mark=markers[selectedPastedImage];var def=mark.Definition;
            MarkerProperties.Visibility=mark.IsPastedImage?Visibility.Collapsed:Visibility.Visible;
            ArrowProperties.Visibility=def?.Kind==MarkerKind.Arrow?Visibility.Visible:Visibility.Collapsed;
            SelectedShadowProperties.Visibility=def?.Kind==MarkerKind.Arrow?Visibility.Visible:Visibility.Collapsed;
            FreehandProperties.Visibility=Visibility.Collapsed;
            TextProperties.Visibility=def?.Kind==MarkerKind.Text?Visibility.Visible:Visibility.Collapsed;
            BlurProperties.Visibility=Visibility.Collapsed;
            markerBrush=mark.Color;
            markerColorKey=mark.Color is SolidColorBrush solid?MarkerColors.Children.OfType<Button>().Where(button=>((string)button.Tag).StartsWith("#")).FirstOrDefault(button=>(Color)ColorConverter.ConvertFromString((string)button.Tag)==solid.Color)?.Tag as string??solid.Color.ToString():FindGradientKey(mark.Color);
            UpdateMarkerColorChecks();
            if(def is not null)markerWidthTool=def.Kind;
            var bitmap=(System.Windows.Media.Imaging.BitmapSource)Photo.Source;
            MarkerWidthSlider.Value=Math.Clamp(mark.Width*700/Math.Min(bitmap.Width,bitmap.Height)/.16,0,100);
            MarkerWidthSlider.IsEnabled=def is not null&&def.Kind!=MarkerKind.Text;
            MarkerStylePicker.SelectedIndex=mark.Contrast?1:0;ArrowShadowSlider.Value=mark.ShadowIntensity*100;
            if(def?.Kind==MarkerKind.Arrow)ArrowFormPicker.SelectedIndex=(int)def.Form;
            if(def?.Kind==MarkerKind.Text)TextSizeSlider.Value=Math.Clamp(def.FontSize*def.ScaleY,12,120);
        }
        finally {syncingElementProperties=false;syncingMarkerWidth=false;}
    }
    private static string FindGradientKey(Brush brush)
    {
        if(brush is not LinearGradientBrush gradient)return "";
        foreach(string key in new[]{"violet","coral","ocean","gold"})
            if(CreateMarkerBrush(key) is LinearGradientBrush candidate&&candidate.GradientStops.Select(stop=>stop.Color).SequenceEqual(gradient.GradientStops.Select(stop=>stop.Color)))return key;
        return "";
    }
    private static Geometry PreviewElementTransform(Geometry geometry,Transform transform)
    {
        var preview=new GeometryGroup {Transform=transform};preview.Children.Add(geometry);preview.Freeze();return preview;
    }
    // Rebuild from endpoints instead of stretching the arrow silhouette.
    private void ResizeArrowEndpoint(int index,bool tail,double dx,double dy)
    {
        if(Photo.Source is not System.Windows.Media.Imaging.BitmapSource bitmap||index<0||index>=markers.Count)return;
        var mark=markers[index];if(mark.Definition is not {Kind:MarkerKind.Arrow} def)return;
        var start=ElementPoint(def,def.Start);var end=ElementPoint(def,def.End);var control=ElementPoint(def,def.Control);
        var next=tail?start:end;
        next=new Point(Math.Clamp(next.X+dx,0,bitmap.Width),Math.Clamp(next.Y+dy,0,bitmap.Height));
        var a=tail?next:start;var b=tail?end:next;
        Vector old=end-start,updated=b-a;if(updated.Length<2||old.Length<.001)return;
        // Keep the bend in the arrow's local coordinate system as length/direction change.
        Vector offset=control-start;
        double along=Vector.Multiply(offset,old)/old.LengthSquared;
        double across=Vector.CrossProduct(old,offset)/old.LengthSquared;
        var bend=a+updated*along+new Vector(-updated.Y,updated.X)*across;
        var normalized=def with {Start=a,End=b,Control=bend,Bounds=new Rect(a,b),Offset=default,ScaleX=1,ScaleY=1};
        markers[index]=RebuildElement(mark with {Definition=normalized});
        RefreshMarkerOverlay();
    }
    private void ResizeMarker(int index,string corner,double dx,double dy)
    {
        if(Photo.Source is not System.Windows.Media.Imaging.BitmapSource bitmap)return;
        var mark=markers[index];var bounds=EditableMarkerBounds(mark);if(bounds.Width<=0||bounds.Height<=0)return;
        bool west=corner.Contains('w'),north=corner.Contains('n');double ratio=bounds.Width/bounds.Height;
        double ax=west?bounds.Right:bounds.Left,ay=north?bounds.Bottom:bounds.Top;
        double maxW=west?ax:bitmap.Width-ax,maxH=north?ay:bitmap.Height-ay;if(maxW<=0||maxH<=0)return;
        double width=Math.Clamp(bounds.Width+(west?-dx:dx),Math.Min(4,maxW),maxW);
        double height=Math.Clamp(bounds.Height+(north?-dy:dy),Math.Min(4,maxH),maxH);
        if(mark.Definition?.Kind==MarkerKind.Text||Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            double changeX=west?-dx:dx,changeY=(north?-dy:dy)*ratio;
            width=Math.Clamp(bounds.Width+(Math.Abs(changeX)>=Math.Abs(changeY)?changeX:changeY),Math.Min(4,Math.Min(maxW,maxH*ratio)),Math.Min(maxW,maxH*ratio));height=width/ratio;
        }
        double x=west?ax-width:ax,y=north?ay-height:ay,sx=width/bounds.Width,sy=height/bounds.Height;
        var origin=pastedImageGesture?.Before??mark;var originalBounds=EditableMarkerBounds(origin);
        sx=width/originalBounds.Width;sy=height/originalBounds.Height;
        double tx=x-originalBounds.Left*sx,ty=y-originalBounds.Top*sy;
        var transform=new MatrixTransform(sx,0,0,sy,tx,ty);
        var geometry=PreviewElementTransform(origin.Geometry,transform);
        Point Map(Point point)=>new(point.X*sx+tx,point.Y*sy+ty);
        var curve=origin.Curve is {} c?new ArrowShape(Map(c.Start),Map(c.End),Map(c.Control)):null;
        markers[index]=origin with {Geometry=geometry,Curve=curve,Width=origin.Width*Math.Sqrt(sx*sy),Definition=origin.Definition is {} def?TransformDefinition(def,sx,sy,tx,ty):null};
        RefreshMarkerOverlay();
    }
    private void DuplicateSelectedElement()
    {
        if(!HasSelectedEditableMarker)return;
        var before=markers.ToArray();var mark=markers[selectedPastedImage];
        double dx=16,dy=16;var bounds=EditableMarkerBounds(mark);var bitmap=(System.Windows.Media.Imaging.BitmapSource)Photo.Source;
        dx=Math.Clamp(bounds.Left+dx,0,Math.Max(0,bitmap.Width-bounds.Width))-bounds.Left;
        dy=Math.Clamp(bounds.Top+dy,0,Math.Max(0,bitmap.Height-bounds.Height))-bounds.Top;
        var moved=Geometry.Combine(mark.Geometry,Geometry.Empty,GeometryCombineMode.Union,new TranslateTransform(dx,dy));moved.Freeze();
        var patch=mark.PatchBounds;if(mark.Patch is not null)patch.Offset(dx,dy);
        var curve=mark.Curve is {} c?new ArrowShape(c.Start+new Vector(dx,dy),c.End+new Vector(dx,dy),c.Control+new Vector(dx,dy)):null;
        markers.Add(mark with {Geometry=moved,PatchBounds=patch,Curve=curve,Definition=mark.Definition is {} def?TransformDefinition(def,1,1,dx,dy):null});
        selectedPastedImage=markers.Count-1;RecordElementEdit(before,"duplicate:"+Guid.NewGuid());
        RefreshMarkerOverlay();SyncSelectedElementProperties();UpdatePendingSaveIndicator();
    }
}
