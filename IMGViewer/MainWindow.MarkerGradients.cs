using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace IMGViewer;
public partial class MainWindow
{
    private string markerColorKey="violet";
    private Point markerEnd;
    private static Brush CreateMarkerBrush(string key)
    {
        if(key.StartsWith('#')){var solid=new SolidColorBrush((Color)ColorConverter.ConvertFromString(key));solid.Freeze();return solid;}
        var colors=key switch {
            "violet"=>("#8B5CF6","#F36DAE"),"coral"=>("#F24F57","#FFAA48"),
            "ocean"=>("#357CF6","#28D8BF"),"gold"=>("#F17A26","#F9D84A"),
            _=>throw new ArgumentException("Gradiente no disponible.",nameof(key))};
        var brush=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(colors.Item1),(Color)ColorConverter.ConvertFromString(colors.Item2),new Point(0,0),new Point(1,1));
        brush.Freeze();return brush;
    }
    private Brush MarkerPaint(Geometry geometry)
    {
        if(markerBrush is not LinearGradientBrush gradient || markerDrag is not {Kind:MarkerKind.Arrow} drag)return markerBrush;
        var bounds=geometry.Bounds;if(bounds.Width<=0||bounds.Height<=0)return markerBrush;
        var brush=gradient.Clone();
        brush.StartPoint=new Point((drag.Start.X-bounds.X)/bounds.Width,(drag.Start.Y-bounds.Y)/bounds.Height);
        brush.EndPoint=new Point((markerEnd.X-bounds.X)/bounds.Width,(markerEnd.Y-bounds.Y)/bounds.Height);
        brush.Freeze();return brush;
    }
    private sealed record DraftGradientStop(string Color,double Offset);
    private sealed record DraftGradient(Point Start,Point End,DraftGradientStop[] Stops);
    private static string DraftBrushColor(Brush brush)=>
        (brush is SolidColorBrush solid?solid.Color:((LinearGradientBrush)brush).GradientStops[0].Color).ToString(CultureInfo.InvariantCulture);
    private static DraftGradient? DraftBrushGradient(Brush brush)=>brush is LinearGradientBrush gradient
        ?new(gradient.StartPoint,gradient.EndPoint,gradient.GradientStops.Select(stop=>new DraftGradientStop(stop.Color.ToString(CultureInfo.InvariantCulture),stop.Offset)).ToArray()):null;
    private static Brush RestoreDraftBrush(DraftMark mark)
    {
        Brush brush=mark.Gradient is {} gradient
            ?new LinearGradientBrush(new GradientStopCollection(gradient.Stops.Select(stop=>new GradientStop((Color)ColorConverter.ConvertFromString(stop.Color),stop.Offset))),gradient.Start,gradient.End)
            :new SolidColorBrush((Color)ColorConverter.ConvertFromString(mark.Color));
        brush.Opacity=mark.Opacity;brush.Freeze();return brush;
    }
}
