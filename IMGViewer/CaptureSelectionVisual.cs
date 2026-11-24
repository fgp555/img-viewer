using System.Windows;
using System.Windows.Media;

namespace IMGViewer;

// Retained drawings update independently of the host layout and frozen desktop image.
public sealed class CaptureSelectionVisual : FrameworkElement
{
    private static readonly Brush Shade=Frozen(new SolidColorBrush(Color.FromArgb(153,0,0,0)));
    private static readonly Pen Edge=Frozen(new Pen(new SolidColorBrush(Color.FromRgb(255,194,71)),1.5) {DashStyle=new DashStyle(new double[]{5,3},0)});
    private static readonly Pen Guide=Frozen(new Pen(new SolidColorBrush(Color.FromArgb(204,255,194,71)),1));
    private readonly DrawingVisual selectionVisual=new();
    private readonly DrawingVisual pointerVisual=new();
    public CaptureSelectionVisual()
    {
        AddVisualChild(selectionVisual);AddVisualChild(pointerVisual);
    }
    protected override int VisualChildrenCount=>2;
    protected override Visual GetVisualChild(int index)=>index switch
    {
        0=>selectionVisual,1=>pointerVisual,_=>throw new ArgumentOutOfRangeException(nameof(index))
    };
    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);DrawSelection();DrawPointer();
    }
    private Rect selection=Rect.Empty;
    private Point? pointer;
    internal Rect Selection=>selection;
    private static T Frozen<T>(T value) where T:Freezable {value.Freeze();return value;}
    internal void SetSelection(Rect value)
    {
        if(selection==value)return;
        selection=value;DrawSelection();
    }
    internal void SetPointer(Point? value)
    {
        if(pointer==value)return;
        pointer=value;DrawPointer();
    }
    private void DrawSelection()
    {
        using var drawing=selectionVisual.RenderOpen();
        Rect full=new(new Point(),RenderSize),hole=selection;
        hole.Intersect(full);
        if(hole.IsEmpty||hole.Width<=0||hole.Height<=0)drawing.DrawRectangle(Shade,null,full);
        else
        {
            drawing.DrawRectangle(Shade,null,new Rect(0,0,full.Width,hole.Top));
            drawing.DrawRectangle(Shade,null,new Rect(0,hole.Bottom,full.Width,Math.Max(0,full.Height-hole.Bottom)));
            drawing.DrawRectangle(Shade,null,new Rect(0,hole.Top,hole.Left,hole.Height));
            drawing.DrawRectangle(Shade,null,new Rect(hole.Right,hole.Top,Math.Max(0,full.Width-hole.Right),hole.Height));
            drawing.DrawRectangle(null,Edge,hole);
        }
    }
    private void DrawPointer()
    {
        using var drawing=pointerVisual.RenderOpen();
        if(pointer is {} point)
        {
            Rect full=new(new Point(),RenderSize);
            drawing.DrawLine(Guide,new Point(0,point.Y),new Point(full.Width,point.Y));
            drawing.DrawLine(Guide,new Point(point.X,0),new Point(point.X,full.Height));
        }
    }
}
