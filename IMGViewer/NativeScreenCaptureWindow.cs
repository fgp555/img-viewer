using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D=System.Drawing;
using F=System.Windows.Forms;

namespace IMGViewer;

// Native capture surface: cached opaque pixels, native input and GDI repainting.
internal sealed class NativeScreenCaptureWindow : F.Form
{
    private DesktopSnapshot snapshot;
    private IReadOnlyDictionary<int,IReadOnlyList<Int32Rect>> regions=new Dictionary<int,IReadOnlyList<Int32Rect>>();
    private List<Int32Rect> candidates=[];
    private int candidateIndex;
    private D.Rectangle detectedSelection,fullscreenButton;
    private bool manualDrag,previewMonitor,fullscreenHovered,fullscreenPressed;
    private readonly D.Font fullscreenIconFont=new("Segoe Fluent Icons",11);
    private Int32Rect monitor;
    private readonly CaptureSelectionMetrics metrics=new("native-gdi");
    internal event EventHandler? ManualSelectionStarted;
    internal System.Windows.Point CaptureOrigin {get;private set;}
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
    internal NativeScreenCaptureWindow(DesktopSnapshot value)
    {
        snapshot=value;monitor=value.MonitorAt(DesktopSnapshot.CursorPosition());
        Text="Capturar pantalla";
        AutoScaleMode=F.AutoScaleMode.None;FormBorderStyle=F.FormBorderStyle.None;
        StartPosition=F.FormStartPosition.Manual;TopMost=true;ShowInTaskbar=false;
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
        if(originalHandle==0||dimmedHandle==0||originalDc==0||dimmedDc==0){Dispose();throw new System.ComponentModel.Win32Exception();}
        oldOriginal=SelectObject(originalDc,originalHandle);oldDimmed=SelectObject(dimmedDc,dimmedHandle);
    }
    protected override void OnDpiChanged(F.DpiChangedEventArgs e) {e.Cancel=true;base.OnDpiChanged(e);}
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);DesktopSnapshot.PlaceOverlay(Handle,snapshot.Bounds);Activate();Focus();
        PositionFullscreenButton();UpdateDetectedSelection(PointToClient(F.Cursor.Position));
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
        if(start is null)
        {
            using var fill=new D.SolidBrush(D.Color.FromArgb(24,24,24));
            using var buttonFill=new D.SolidBrush(fullscreenPressed?D.Color.FromArgb(40,40,40):fullscreenHovered?D.Color.FromArgb(53,53,53):D.Color.FromArgb(24,24,24));
            e.Graphics.FillRectangle(buttonFill,fullscreenButton);
            var textBounds=new D.Rectangle(fullscreenButton.X+8,fullscreenButton.Y,fullscreenButton.Width-36,fullscreenButton.Height);
            var iconBounds=new D.Rectangle(fullscreenButton.Right-29,fullscreenButton.Y,22,fullscreenButton.Height);
            F.TextRenderer.DrawText(e.Graphics,"\uE740",fullscreenIconFont,iconBounds,D.Color.Gold,
                F.TextFormatFlags.HorizontalCenter|F.TextFormatFlags.VerticalCenter|F.TextFormatFlags.NoPadding);
            F.TextRenderer.DrawText(e.Graphics,"Pantalla completa",Font,textBounds,D.Color.White,
                F.TextFormatFlags.HorizontalCenter|F.TextFormatFlags.VerticalCenter|F.TextFormatFlags.NoPadding);

        }
        if(!dimensionsBounds.IsEmpty)
        {
            using var fill=new D.SolidBrush(D.Color.FromArgb(24,24,24));
            e.Graphics.FillRectangle(fill,dimensionsBounds);
            F.TextRenderer.DrawText(e.Graphics,DimensionsText,Font,dimensionsBounds,D.Color.White,
                F.TextFormatFlags.HorizontalCenter|F.TextFormatFlags.VerticalCenter|F.TextFormatFlags.NoPadding);
        }
    }
    internal void UpdateDetectedAreas(DesktopSnapshot detected,IReadOnlyDictionary<int,IReadOnlyList<Int32Rect>> areas)
    {
        if(IsDisposed || start is not null)return;
        snapshot=detected;regions=areas;UpdateDetectedSelection(PointToClient(F.Cursor.Position));
    }
    private D.Rectangle Local(Int32Rect area)=>new(area.X-snapshot.Bounds.X,area.Y-snapshot.Bounds.Y,area.Width,area.Height);
    private void PositionFullscreenButton()
    {
        var local=Local(monitor);
        fullscreenButton=new D.Rectangle(local.X+Math.Max(0,(local.Width-160)/2),local.Y+12,160,28);
    }
    private void SetFullscreenHover(bool hovered)
    {
        Cursor=hovered?F.Cursors.Hand:F.Cursors.Cross;
        if(fullscreenHovered==hovered)return;
        fullscreenHovered=hovered;Invalidate(fullscreenButton);
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if(start is not null||fullscreenPressed)return;
        SetFullscreenHover(false);previewMonitor=false;SetSelection(detectedSelection);
    }
    private void UpdateDetectedSelection(D.Point point)
    {
        var physical=new System.Windows.Point(snapshot.Bounds.X+point.X,snapshot.Bounds.Y+point.Y);
        var nextMonitor=snapshot.MonitorAt(physical);
        if(nextMonitor!=monitor){var oldButton=fullscreenButton;monitor=nextMonitor;PositionFullscreenButton();Invalidate(oldButton);Invalidate(fullscreenButton);}
        SetFullscreenHover(fullscreenButton.Contains(point));
        if(fullscreenHovered)
        {previewMonitor=true;SetSelection(Local(monitor));return;}
        bool leavingPreview=previewMonitor;
        previewMonitor=false;
        var next=new List<Int32Rect>();
        for(int i=0;i<snapshot.Windows.Count;i++)
        {
            var window=snapshot.Windows[i];if(!Local(window).Contains(point))continue;
            if(regions.TryGetValue(i,out var areas))next.AddRange(areas.Where(area=>Local(area).Contains(point)));
            next.Add(window);break;
        }
        next=next.Distinct().OrderBy(area=>(long)area.Width*area.Height).ToList();
        if(leavingPreview && next.Count==0){SetSelection(detectedSelection);return;}
        if(!next.SequenceEqual(candidates))candidateIndex=0;
        candidates=next;detectedSelection=next.Count==0?D.Rectangle.Empty:Local(next[candidateIndex]);
        if(selection!=detectedSelection){SetSelection(detectedSelection);Invalidate();}
    }
    protected override void OnMouseDown(F.MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=F.MouseButtons.Left)return;
        if(fullscreenButton.Contains(e.Location))
        {
            fullscreenPressed=true;Capture=true;SetFullscreenHover(true);Invalidate(fullscreenButton);return;
        }
        start=e.Location;manualDrag=false;Capture=true;
        ManualSelectionStarted?.Invoke(this,EventArgs.Empty);metrics.StartDrag();Invalidate();
    }
    protected override void OnMouseMove(F.MouseEventArgs e)
    {
        base.OnMouseMove(e);var next=PointToClient(F.Cursor.Position);
        if(next==pointer)return;
        metrics.Sample(start is not null);
        Invalidate(new D.Rectangle(0,pointer.Y,ClientSize.Width,2));Invalidate(new D.Rectangle(pointer.X,0,2,ClientSize.Height));
        pointer=next;
        Invalidate(new D.Rectangle(0,pointer.Y,ClientSize.Width,2));Invalidate(new D.Rectangle(pointer.X,0,2,ClientSize.Height));
        if(fullscreenPressed){SetFullscreenHover(fullscreenButton.Contains(next));return;}
        if(start is {} a)
        {
            manualDrag|=Math.Abs(next.X-a.X)>=F.SystemInformation.DragSize.Width || Math.Abs(next.Y-a.Y)>=F.SystemInformation.DragSize.Height;
            if(manualDrag)SetSelection(DragBounds(a,next));
        }
        else UpdateDetectedSelection(next);
    }
    protected override void OnMouseWheel(F.MouseEventArgs e)
    {
        base.OnMouseWheel(e);if(start is not null||previewMonitor||candidates.Count==0)return;
        candidateIndex=(candidateIndex+(e.Delta<0?1:-1)+candidates.Count)%candidates.Count;
        detectedSelection=Local(candidates[candidateIndex]);SetSelection(detectedSelection);Invalidate();
    }
    internal static D.Rectangle DragBounds(D.Point a,D.Point b)=>D.Rectangle.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y));
    protected override void OnMouseUp(F.MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if(e.Button!=F.MouseButtons.Left)return;
        if(fullscreenPressed)
        {
            bool activate=fullscreenButton.Contains(e.Location);fullscreenPressed=false;Capture=false;
            SetFullscreenHover(activate);Invalidate(fullscreenButton);
            if(activate)Complete(Local(monitor));else UpdateDetectedSelection(e.Location);
            return;
        }
        if(start is not {} a)return;
        bool custom=manualDrag||Math.Abs(e.X-a.X)>=F.SystemInformation.DragSize.Width||Math.Abs(e.Y-a.Y)>=F.SystemInformation.DragSize.Height;
        var area=custom?DragBounds(a,e.Location):detectedSelection;
        start=null;manualDrag=false;Capture=false;Complete(area);
    }
    private void Complete(D.Rectangle area)
    {
        area=D.Rectangle.Intersect(area,new D.Rectangle(0,0,imageWidth,imageHeight));
        if(area.Width<=0||area.Height<=0)return;
        var physical=new Int32Rect(snapshot.Bounds.X+area.X,snapshot.Bounds.Y+area.Y,area.Width,area.Height);
        CaptureOrigin=new System.Windows.Point(physical.X+physical.Width/2.0,physical.Y+physical.Height/2.0);
        Result=snapshot.Crop(physical);DialogResult=F.DialogResult.OK;Close();
    }
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if(fullscreenPressed&&!Capture){fullscreenPressed=false;SetFullscreenHover(false);Invalidate(fullscreenButton);}
        if(start is not null&&!Capture){start=null;manualDrag=false;SetSelection(D.Rectangle.Empty);Invalidate();}
    }
    protected override void OnKeyDown(F.KeyEventArgs e)
    {
        if(e.KeyCode==F.Keys.Escape)
        {
            e.Handled=e.SuppressKeyPress=true;
            if(fullscreenPressed){fullscreenPressed=false;Capture=false;SetFullscreenHover(false);Invalidate(fullscreenButton);}
            else if(start is not null){start=null;manualDrag=false;Capture=false;SetSelection(D.Rectangle.Empty);Invalidate();}
            else {DialogResult=F.DialogResult.Cancel;Close();}
        }
        else if(e.KeyCode==F.Keys.Enter){e.Handled=true;Complete(Local(monitor));}
        base.OnKeyDown(e);
    }
    protected override void OnFormClosed(F.FormClosedEventArgs e)
    {
        metrics.Finish(imageWidth,imageHeight,RenderCapability.Tier>>16);base.OnFormClosed(e);
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
            edge.Dispose();guide.Dispose();fullscreenIconFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
