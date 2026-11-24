using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D=System.Drawing;
using F=System.Windows.Forms;

namespace IMGViewer;

// Isolated native-window prototype: no layered HWND, WPF layout or desktop blend per frame.
internal sealed class NativeSelector : F.Form
{
    private readonly DesktopSnapshot snapshot;
    private readonly int imageWidth,imageHeight;
    private readonly nint originalDc,dimmedDc,originalHandle,dimmedHandle,oldOriginal,oldDimmed;
    private bool resourcesDisposed;
    private D.Point? start;
    private D.Point pointer;
    private D.Rectangle selection;
    private D.Rectangle dimensionsBounds,cachedMonitor;
    internal string DimensionsText {get;private set;}="";
    internal D.Rectangle DimensionsBounds=>dimensionsBounds;
    private readonly D.Pen edge=new(D.Color.Gold,1) {DashStyle=D.Drawing2D.DashStyle.Dash};
    private readonly D.Pen guide=new(D.Color.Gold,1);
    internal BitmapSource? Result {get;private set;}
    internal List<double> DrawMs {get;}=[];
    internal event Action? Painted;
    internal NativeSelector(DesktopSnapshot value)
    {
        snapshot=value;
        Text="IMGViewer · Selector nativo de prueba";
        AutoScaleMode=F.AutoScaleMode.None;FormBorderStyle=F.FormBorderStyle.None;
        StartPosition=F.FormStartPosition.Manual;TopMost=true;ShowInTaskbar=true;
        Bounds=new D.Rectangle(value.Bounds.X,value.Bounds.Y,value.Bounds.Width,value.Bounds.Height);
        Cursor=F.Cursors.Cross;KeyPreview=true;
        SetStyle(F.ControlStyles.UserPaint|F.ControlStyles.AllPaintingInWmPaint|F.ControlStyles.OptimizedDoubleBuffer|F.ControlStyles.Opaque,true);
        var source=new FormatConvertedBitmap(value.Bitmap,PixelFormats.Bgr32,null,0);
        int stride=checked(source.PixelWidth*4);var bytes=new byte[checked(stride*source.PixelHeight)];source.CopyPixels(bytes,stride,0);
        using var original=new D.Bitmap(source.PixelWidth,source.PixelHeight,D.Imaging.PixelFormat.Format32bppRgb);
        var bits=original.LockBits(new D.Rectangle(0,0,original.Width,original.Height),D.Imaging.ImageLockMode.WriteOnly,D.Imaging.PixelFormat.Format32bppRgb);
        try {for(int y=0;y<original.Height;y++)Marshal.Copy(bytes,y*stride,bits.Scan0+y*bits.Stride,stride);}
        finally {original.UnlockBits(bits);}
        using var dimmed=(D.Bitmap)original.Clone();
        using(var graphics=D.Graphics.FromImage(dimmed))using(var shade=new D.SolidBrush(D.Color.FromArgb(153,0,0,0)))
            graphics.FillRectangle(shade,0,0,dimmed.Width,dimmed.Height);
        imageWidth=original.Width;imageHeight=original.Height;
        originalHandle=original.GetHbitmap();dimmedHandle=dimmed.GetHbitmap();
        originalDc=CreateCompatibleDC(0);dimmedDc=CreateCompatibleDC(0);
        if(originalHandle==0||dimmedHandle==0||originalDc==0||dimmedDc==0)throw new System.ComponentModel.Win32Exception();
        oldOriginal=SelectObject(originalDc,originalHandle);oldDimmed=SelectObject(dimmedDc,dimmedHandle);
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);DesktopSnapshot.PlaceOverlay(Handle,snapshot.Bounds);Activate();
    }
    internal void SetSelection(D.Rectangle value)
    {
        var oldLabel=dimensionsBounds;
        var old=selection;selection=D.Rectangle.Intersect(value,new D.Rectangle(0,0,imageWidth,imageHeight));
        if(selection.Width>0&&selection.Height>0)
        {
            DimensionsText=$"{selection.Width} × {selection.Height} px";
            var corner=new D.Point(selection.Right-1,selection.Bottom-1);
            if(!cachedMonitor.Contains(corner))
            {
                var screen=snapshot.MonitorAt(new Point(snapshot.Bounds.X+corner.X,snapshot.Bounds.Y+corner.Y));
                cachedMonitor=new D.Rectangle(screen.X-snapshot.Bounds.X,screen.Y-snapshot.Bounds.Y,screen.Width,screen.Height);
            }
            dimensionsBounds=PlaceDimensions(selection,cachedMonitor);
        }
        else {DimensionsText="";dimensionsBounds=D.Rectangle.Empty;}
        if(!oldLabel.IsEmpty)Invalidate(oldLabel);
        if(!dimensionsBounds.IsEmpty)Invalidate(dimensionsBounds);
        var dirty=D.Rectangle.Union(old,selection);dirty.Inflate(3,3);Invalidate(dirty);
    }
    internal static D.Rectangle PlaceDimensions(D.Rectangle area,D.Rectangle screen)
    {
        const int width=160,height=28,gap=8;
        int x=area.Right-width,y=area.Bottom+gap;
        if(y+height>screen.Bottom-gap)y=area.Bottom-height-gap;
        x=Math.Clamp(x,screen.Left+gap,Math.Max(screen.Left+gap,screen.Right-width-gap));
        y=Math.Clamp(y,screen.Top+gap,Math.Max(screen.Top+gap,screen.Bottom-height-gap));
        return new D.Rectangle(x,y,width,height);
    }
    protected override void OnPaintBackground(F.PaintEventArgs e) { }
    protected override void OnPaint(F.PaintEventArgs e)
    {
        long tick=Stopwatch.GetTimestamp();
        var clip=D.Rectangle.Intersect(e.ClipRectangle,new D.Rectangle(0,0,imageWidth,imageHeight));
        nint dc=e.Graphics.GetHdc();
        try
        {
            if(clip.Width>0&&clip.Height>0)BitBlt(dc,clip.X,clip.Y,clip.Width,clip.Height,dimmedDc,clip.X,clip.Y,0x00CC0020);
            var revealed=D.Rectangle.Intersect(selection,clip);
            if(revealed.Width>0&&revealed.Height>0)BitBlt(dc,revealed.X,revealed.Y,revealed.Width,revealed.Height,originalDc,revealed.X,revealed.Y,0x00CC0020);
        }
        finally {e.Graphics.ReleaseHdc(dc);}
        if(selection.Width>0&&selection.Height>0)
        {
            e.Graphics.DrawRectangle(edge,selection.X,selection.Y,Math.Max(0,selection.Width-1),Math.Max(0,selection.Height-1));
        }
        e.Graphics.DrawLine(guide,0,pointer.Y,ClientSize.Width,pointer.Y);
        e.Graphics.DrawLine(guide,pointer.X,0,pointer.X,ClientSize.Height);
        if(!dimensionsBounds.IsEmpty)
        {
            using var fill=new D.SolidBrush(D.Color.FromArgb(24,24,24));
            e.Graphics.FillRectangle(fill,dimensionsBounds);
            F.TextRenderer.DrawText(e.Graphics,DimensionsText,Font,dimensionsBounds,D.Color.White,
                F.TextFormatFlags.HorizontalCenter|F.TextFormatFlags.VerticalCenter|F.TextFormatFlags.NoPadding);
        }
        DrawMs.Add(Stopwatch.GetElapsedTime(tick).TotalMilliseconds);Painted?.Invoke();
    }
    protected override void OnMouseDown(F.MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=F.MouseButtons.Left)return;
        start=e.Location;Capture=true;
    }
    protected override void OnMouseMove(F.MouseEventArgs e)
    {
        base.OnMouseMove(e);var next=PointToClient(F.Cursor.Position);
        // Invalidate only the old/new one-pixel crosshair strips; WM_PAINT coalesces input.
        Invalidate(new D.Rectangle(0,pointer.Y,ClientSize.Width,2));Invalidate(new D.Rectangle(pointer.X,0,2,ClientSize.Height));
        pointer=next;
        Invalidate(new D.Rectangle(0,pointer.Y,ClientSize.Width,2));Invalidate(new D.Rectangle(pointer.X,0,2,ClientSize.Height));
        if(start is {} a)SetSelection(DragBounds(a,next));
    }
    internal static D.Rectangle DragBounds(D.Point a,D.Point b)=>D.Rectangle.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y));
    protected override void OnMouseUp(F.MouseEventArgs e)
    {
        base.OnMouseUp(e);if(e.Button!=F.MouseButtons.Left||start is not {} a)return;
        SetSelection(DragBounds(a,e.Location));start=null;Capture=false;
        if(selection.Width<=0||selection.Height<=0)return;
        Result=snapshot.Crop(new Int32Rect(snapshot.Bounds.X+selection.X,snapshot.Bounds.Y+selection.Y,selection.Width,selection.Height));
        DialogResult=F.DialogResult.OK;Close();
    }
    protected override void OnKeyDown(F.KeyEventArgs e)
    {
        if(e.KeyCode==F.Keys.Escape)
        {
            e.Handled=true;
            if(start is not null){start=null;Capture=false;SetSelection(D.Rectangle.Empty);}
            else {DialogResult=F.DialogResult.Cancel;Close();}
        }
        else if(e.KeyCode==F.Keys.Enter)
        {
            var monitor=snapshot.MonitorAt(DesktopSnapshot.CursorPosition());Result=snapshot.Crop(monitor);DialogResult=F.DialogResult.OK;Close();
        }
        base.OnKeyDown(e);
    }
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc,nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint dst,int x,int y,int w,int h,nint src,int sx,int sy,uint mode);
    protected override void Dispose(bool disposing)
    {
        if(disposing && !resourcesDisposed){
            resourcesDisposed=true;
            if(originalDc!=0){SelectObject(originalDc,oldOriginal);DeleteDC(originalDc);}
            if(dimmedDc!=0){SelectObject(dimmedDc,oldDimmed);DeleteDC(dimmedDc);}
            if(originalHandle!=0)DeleteObject(originalHandle);if(dimmedHandle!=0)DeleteObject(dimmedHandle);
            edge.Dispose();guide.Dispose();
        }
        base.Dispose(disposing);
    }
}
