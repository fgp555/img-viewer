using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using D=System.Drawing;
using F=System.Windows.Forms;

namespace IMGViewer;
internal static class ProbeProgram
{
    private sealed record Measurement(string Engine,int Width,int Height,int Samples,double MedianTickMs,double P95TickMs,double MaxTickMs,double ProcessCpuMs,long WorkingSetBytes,double PreparationMs);
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            if(args.Contains("--check"))
            {
                var rect=NativeSelector.DragBounds(new D.Point(300,200),new D.Point(100,50));
                if(rect!=new D.Rectangle(100,50,200,150))throw new Exception("Reverse drag mapping failed");
                var snap=Synthetic();using var form=new NativeSelector(snap);
                form.SetSelection(new D.Rectangle(-20,-10,60,50));
                if(form.DimensionsText!="40 × 40 px")throw new Exception("Clipped dimensions incorrect");
                form.SetSelection(rect);
                if(form.DimensionsText!="200 × 150 px")throw new Exception("Live dimensions did not update");
                if(!new D.Rectangle(0,0,1280,720).Contains(form.DimensionsBounds))throw new Exception("Dimensions outside screen");
                var edgeLabel=NativeSelector.PlaceDimensions(new D.Rectangle(1000,600,280,120),new D.Rectangle(0,0,1280,720));
                if(edgeLabel.Bottom>720||edgeLabel.Right>1280)throw new Exception("Edge label placement failed");
                form.SetSelection(D.Rectangle.Empty);
                if(form.DimensionsText!=""||!form.DimensionsBounds.IsEmpty)throw new Exception("Canceled dimensions remained visible");
                var crop=snap.Crop(new Int32Rect(-100,-50,40,40));
                if(crop.PixelWidth!=40||crop.PixelHeight!=40)throw new Exception("Negative desktop mapping failed");
                Console.WriteLine("PASS: reverse drag, clipping, negative desktop origin, cropped dimensions, live label updates, edge placement and label cancellation. No user files modified.");return 0;
            }
            // Exactly one frozen source is reused for both engines, including the virtual-desktop bounds.
            var snapshot=args.Contains("--synthetic")?Synthetic():DesktopSnapshot.Capture();
            if(args.Contains("--benchmark"))
            {
                Measurement native,wpf; if(args.Contains("--wpf-first")){wpf=OnSta(()=>BenchmarkWpf(snapshot));native=OnSta(()=>BenchmarkNative(snapshot));} else {native=OnSta(()=>BenchmarkNative(snapshot));wpf=OnSta(()=>BenchmarkWpf(snapshot));}
                Console.WriteLine(JsonSerializer.Serialize(new {Source="Same frozen desktop; no image saved",Order=args.Contains("--wpf-first")?"WPF then native":"native then WPF",Native=native,Wpf=wpf,
                    Limit="Scripted 120-step selection; tick intervals are scheduling measurements, not mouse-to-display latency. Process memory includes shared runtime/caches; order is reported separately."},new JsonSerializerOptions{WriteIndented=true}));
                return 0;
            }
            Console.WriteLine("Native selector: drag to select; Esc cancels drag, second Esc exits; Enter captures monitor. No files or clipboard writes.");
            OnSta(()=>{using var selector=new NativeSelector(snapshot);selector.ShowDialog();Console.WriteLine("Native result: "+Describe(selector.Result));return 0;});
            if(!args.Contains("--native"))
            {
                Console.WriteLine("WPF selector: same frozen image. Drag to select or Esc to exit.");
                OnSta(()=>{var selector=new ScreenCaptureWindow(snapshot,new Dictionary<int,IReadOnlyList<Int32Rect>>());selector.ShowDialog();Console.WriteLine("WPF result: "+Describe(selector.Result));return 0;});
            }
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static string Describe(BitmapSource? bitmap)=>bitmap is null?"Canceled":$"{bitmap.PixelWidth}x{bitmap.PixelHeight}";
    private static T OnSta<T>(Func<T> action)
    {
        T value=default!;Exception? error=null;
        var thread=new Thread(()=>{try{value=action();}catch(Exception ex){error=ex;}finally{Dispatcher.CurrentDispatcher.InvokeShutdown();}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error is not null)throw error;return value;
    }
    private static DesktopSnapshot Synthetic()
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
            dc.DrawRectangle(Brushes.DarkSlateGray,null,new Rect(0,0,1280,720));
            dc.DrawRoundedRectangle(Brushes.SteelBlue,null,new Rect(80,160,600,400),12,12);
        }
        var bitmap=new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();
        return new DesktopSnapshot(bitmap,new Int32Rect(-100,-50,1280,720),[],[]);
    }
    private static Measurement BenchmarkNative(DesktopSnapshot snapshot)
    {
        using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;
        var prep=Stopwatch.StartNew();using var form=new NativeSelector(snapshot);prep.Stop();
        using var timer=new F.Timer{Interval=17};var clock=new Stopwatch();var gaps=new List<double>();double last=0;int frame=0;
        form.Shown+=(_,_)=>{clock.Start();timer.Start();};
        timer.Tick+=(_,_)=>{
            double now=clock.Elapsed.TotalMilliseconds;if(frame>0)gaps.Add(now-last);last=now;
            form.SetSelection(Trajectory(snapshot,frame));
            if(++frame>=120){timer.Stop();form.Close();}
        };
        form.ShowDialog();process.Refresh();return Summary("Win32 / Windows Forms / GDI",snapshot,gaps,(process.TotalProcessorTime-cpu).TotalMilliseconds,process.WorkingSet64,prep.Elapsed.TotalMilliseconds);
    }
    private static Measurement BenchmarkWpf(DesktopSnapshot snapshot)
    {
        using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;
        var prep=Stopwatch.StartNew();var window=new ScreenCaptureWindow(snapshot,new Dictionary<int,IReadOnlyList<Int32Rect>>());prep.Stop();
        var type=window.GetType();const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var show=type.GetMethod("ShowSelection",flags)!;type.GetField("dragStart",flags)!.SetValue(window,new Point(0,0));
        type.GetField("draggingRegion",flags)!.SetValue(window,true);
        var timer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(17)};
        var clock=new Stopwatch();var gaps=new List<double>();double last=0;int frame=0;
        window.ContentRendered+=(_,_)=>{clock.Start();timer.Start();};
        timer.Tick+=(_,_)=>{
            double now=clock.Elapsed.TotalMilliseconds;if(frame>0)gaps.Add(now-last);last=now;
            var rect=Trajectory(snapshot,frame);show.Invoke(window,[new Int32Rect(snapshot.Bounds.X+rect.X,snapshot.Bounds.Y+rect.Y,rect.Width,rect.Height)]);
            if(++frame>=120){timer.Stop();window.DialogResult=false;}
        };
        window.ShowDialog();process.Refresh();return Summary("WPF / DrawingVisual",snapshot,gaps,(process.TotalProcessorTime-cpu).TotalMilliseconds,process.WorkingSet64,prep.Elapsed.TotalMilliseconds);
    }
    private static D.Rectangle Trajectory(DesktopSnapshot snapshot,int frame)
    {
        int x=Math.Max(1,snapshot.Bounds.Width/10),y=Math.Max(1,snapshot.Bounds.Height/10);
        double t=(Math.Sin(frame*Math.PI/30)+1)/2;
        return new D.Rectangle(x,y,Math.Max(1,(int)(snapshot.Bounds.Width*(.2+.5*t))),Math.Max(1,(int)(snapshot.Bounds.Height*(.2+.5*t))));
    }
    private static Measurement Summary(string engine,DesktopSnapshot snapshot,List<double> gaps,double cpu,long memory,double prep)
    {
        gaps.Sort();return new(engine,snapshot.Bitmap.PixelWidth,snapshot.Bitmap.PixelHeight,gaps.Count,gaps[gaps.Count/2],gaps[(int)(gaps.Count*.95)],gaps[^1],cpu,memory,prep);
    }
}
