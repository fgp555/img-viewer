using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IMGViewer;
using D=System.Drawing;
using F=System.Windows.Forms;
internal static class Program
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly Assembly Assembly=typeof(MainWindow).Assembly;
    private static readonly Type Native=Assembly.GetType("IMGViewer.NativeScreenCaptureWindow")!;
    private static int checks;
    private static void Check(bool value,string text){if(!value)throw new Exception(text);checks++;}
    private static object? Call(object target,string name,params object?[] args)=>Native.GetMethod(name,Flags)!.Invoke(target,args);
    private static object? Field(object target,string name)=>Native.GetField(name,Flags)!.GetValue(target);
    private static F.MouseEventArgs Mouse(int x,int y)=>new(F.MouseButtons.Left,1,x,y,0);
    private static object Snapshot()
    {
        var pixels=BitmapSource.Create(800,600,96,96,PixelFormats.Bgr32,null,new byte[800*600*4],800*4);pixels.Freeze();
        return Activator.CreateInstance(Assembly.GetType("IMGViewer.DesktopSnapshot")!,Flags,null,
            new object[]{pixels,new Int32Rect(-100,-50,800,600),new List<Int32Rect>{new(-50,30,400,300)},new List<nint>{0}},null)!;
    }
    private static F.Form Form(object snapshot)=>(F.Form)Activator.CreateInstance(Native,Flags,null,new[]{snapshot},null)!;
    private static BitmapSource? Run(object snapshot,Action<F.Form> action)
    {
        using var form=Form(snapshot);Exception? error=null;
        form.Shown+=(_,_)=>form.BeginInvoke(()=>{try{action(form);}catch(Exception ex){error=ex;form.Close();}});
        form.ShowDialog();if(error is not null)throw error;
        return (BitmapSource?)Native.GetProperty("Result",Flags)!.GetValue(form);
    }
    [DllImport("user32.dll")] private static extern uint GetGuiResources(nint process,uint flags);
    [STAThread] private static int Main()
    {
        try
        {
            var snapshot=Snapshot();
            Run(snapshot,form=>{
                Call(form,"OnMouseDown",Mouse(100,100));
                Call(form,"SetSelection",new D.Rectangle(100,100,320,180));
                Check((string)Native.GetProperty("DimensionsText",Flags)!.GetValue(form)! =="320 × 180 px","Live dimensions not shown during drag");
                Call(form,"SetSelection",new D.Rectangle(100,100,400,250));
                Check((string)Native.GetProperty("DimensionsText",Flags)!.GetValue(form)! =="400 × 250 px","Dimensions did not update");
                var label=(D.Rectangle)Native.GetProperty("DimensionsBounds",Flags)!.GetValue(form)!;
                Check(new D.Rectangle(0,0,800,600).Contains(label),"Label outside desktop");
                var edgeLabel=(D.Rectangle)Call(form,"PlaceDimensions",new D.Rectangle(700,500,100,100),new D.Rectangle(0,0,800,600))!;
                Check(edgeLabel.Bottom<=600&&edgeLabel.Right<=800,"Edge label placement failed");
                Call(form,"OnKeyDown",new F.KeyEventArgs(F.Keys.Escape));
                Check((string)Native.GetProperty("DimensionsText",Flags)!.GetValue(form)! =="","Canceled label text remained");
                Check(((D.Rectangle)Native.GetProperty("DimensionsBounds",Flags)!.GetValue(form)!).IsEmpty,"Canceled label remained visible");
                Call(form,"OnKeyDown",new F.KeyEventArgs(F.Keys.Escape));
            });
            var result=Run(snapshot,form=>{
                Call(form,"OnMouseDown",Mouse(300,250));
                Call(form,"OnMouseUp",Mouse(100,100));
            });
            Check(result is {PixelWidth:200,PixelHeight:150},"Reverse drag dimensions incorrect");
            Check(result!.IsFrozen,"Result must cross threads frozen");
            result=Run(snapshot,form=>{
                Call(form,"UpdateDetectedSelection",new D.Point(100,100));
                Check((D.Rectangle)Field(form,"selection")! ==new D.Rectangle(50,80,400,300),"Window detection mapping failed");
                Call(form,"UpdateDetectedAreas",snapshot,new Dictionary<int,IReadOnlyList<Int32Rect>>{{0,new List<Int32Rect>{new(-20,50,100,80)}}});
                // Hardware cursor is independent; choose a point explicitly for this deterministic test.
                Call(form,"UpdateDetectedSelection",new D.Point(100,110));
                Check((D.Rectangle)Field(form,"selection")! ==new D.Rectangle(80,100,100,80),"Nested area selection failed");
                Call(form,"OnMouseWheel",new F.MouseEventArgs(F.MouseButtons.None,0,100,110,-120));
                Check((D.Rectangle)Field(form,"selection")! ==new D.Rectangle(50,80,400,300),"Wheel did not cycle detected areas");
                Call(form,"OnMouseDown",Mouse(100,110));Call(form,"OnMouseUp",Mouse(100,110));
            });
            Run(snapshot,form=>{
                Call(form,"OnMouseDown",Mouse(100,100));
                Call(form,"SetSelection",new D.Rectangle(100,100,200,100));
                Call(form,"OnKeyDown",new F.KeyEventArgs(F.Keys.Escape));
                Check(!form.IsDisposed&&Field(form,"start") is null,"First Escape must cancel drag only");
                Check(((D.Rectangle)Field(form,"selection")!).IsEmpty,"Escape retained manual rectangle");
                Call(form,"OnKeyDown",new F.KeyEventArgs(F.Keys.Escape));
            });
            result=Run(snapshot,form=>{
                Call(form,"OnMouseDown",Mouse(100,100));Call(form,"OnMouseUp",Mouse(900,700));
            });
            Check(result is {PixelWidth:700,PixelHeight:500},"Out-of-bounds selection not clipped");
            result=Run(snapshot,form=>{
                var button=(D.Rectangle)Field(form,"fullscreenButton")!;
                Call(form,"UpdateDetectedSelection",new D.Point(100,100));
                var previous=(D.Rectangle)Field(form,"selection")!;
                Call(form,"UpdateDetectedSelection",new D.Point(button.Left+5,button.Top+5));
                Call(form,"OnMouseLeave",EventArgs.Empty);
                Check((D.Rectangle)Field(form,"selection")! ==previous,"Leaving fullscreen button lost prior selection");
                Check(ReferenceEquals(form.Cursor,F.Cursors.Cross)&&!(bool)Field(form,"fullscreenHovered")!,"Hover did not reset");
                Call(form,"UpdateDetectedSelection",new D.Point(button.Left+5,button.Top+5));
                Call(form,"OnMouseDown",Mouse(button.Left+5,button.Top+5));
                Call(form,"OnMouseUp",Mouse(0,500));
                Check(Native.GetProperty("Result",Flags)!.GetValue(form) is null&&!form.IsDisposed,"Release outside button captured unexpectedly");
                Call(form,"UpdateDetectedSelection",new D.Point(button.Left+5,button.Top+5));
                Check((bool)Field(form,"previewMonitor")!,"Fullscreen button did not preview monitor");
                var preview=(D.Rectangle)Field(form,"selection")!;
                Check(ReferenceEquals(form.Cursor,F.Cursors.Hand),"Fullscreen hover cursor must be hand");
                Call(form,"OnMouseDown",Mouse(button.Left+5,button.Top+5));
                Check((bool)Field(form,"fullscreenPressed")!,"Fullscreen press state missing");
                Check(Native.GetProperty("Result",Flags)!.GetValue(form) is null,"Fullscreen activated before release");
                Call(form,"OnMouseUp",Mouse(button.Left+5,button.Top+5));
                var bitmap=(BitmapSource)Native.GetProperty("Result",Flags)!.GetValue(form)!;
                Check(bitmap.PixelWidth==preview.Width&&bitmap.PixelHeight==preview.Height,"Fullscreen result differs from preview");
            });
            // Exercise the actual capture-session path, not only the native form.
            var session=(Task)Assembly.GetType("IMGViewer.CaptureSelectionSession")!.GetMethod("ShowAsync",Flags)!.Invoke(null,new[]{snapshot})!;
            var limit=Stopwatch.StartNew();F.Form? selected=null;
            while(selected is null&&limit.ElapsedMilliseconds<10000)
            {
                selected=F.Application.OpenForms.Cast<F.Form>().FirstOrDefault(f=>f.GetType()==Native&&f.IsHandleCreated);
                if(selected is null)Thread.Sleep(10);
            }
            Check(selected is not null,"Capture session did not open native selector");
            selected!.BeginInvoke(()=>{Call(selected,"OnMouseDown",Mouse(100,100));Call(selected,"OnMouseUp",Mouse(300,250));});
            Check(session.Wait(10000),"Capture session did not complete");
            var outcome=session.GetType().GetProperty("Result")!.GetValue(session)!;
            Check(outcome is not null,"Native session did not return a capture");
            var actual=(BitmapSource)outcome!.GetType().GetProperty("Bitmap")!.GetValue(outcome)!;
            Check(actual.IsFrozen&&actual.PixelWidth==200&&actual.PixelHeight==150,"Session result invalid");
            var origin=(Point)outcome.GetType().GetProperty("Origin")!.GetValue(outcome)!;
            Check(origin==new Point(100,125),"Physical capture origin incorrect");
            Console.WriteLine($"PASS: {checks} native capture checks — reverse drag, detected windows/areas, wheel, Esc, clipping, fullscreen preview, native session and frozen result. No clipboard or capture-file writes.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
