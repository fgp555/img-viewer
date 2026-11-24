using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace IMGViewer;

internal static class CaptureSelectionSession
{
    internal sealed record Selection(BitmapSource Bitmap,Point Origin);
    internal static Task<Selection?> ShowAsync(DesktopSnapshot snapshot)
    {
        var completion=new TaskCompletionSource<Selection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            var dispatcher=Dispatcher.CurrentDispatcher;
            using var detection=new CancellationTokenSource();
            try
            {
                using var selector=new NativeScreenCaptureWindow(snapshot);
                selector.ManualSelectionStarted+=(_,_)=>detection.Cancel();
                selector.FormClosed+=(_,_)=>detection.Cancel();
                selector.Shown+=async(_,_)=>
                {
                    try
                    {
                        var detected=await Task.Run(snapshot.WithWindowBounds);
                        if(!selector.Visible || selector.IsDisposed || detection.IsCancellationRequested)return;
                        selector.UpdateDetectedAreas(detected,new Dictionary<int,IReadOnlyList<Int32Rect>>());
                        var areas=await CaptureAreaDetector.DetectAsync(detected,detection.Token);
                        if(selector.Visible && !selector.IsDisposed && !detection.IsCancellationRequested)selector.UpdateDetectedAreas(detected,areas);
                    }
                    catch(Exception ex) when(ex is Win32Exception or ExternalException or InvalidOperationException or ArgumentException)
                    {System.Diagnostics.Debug.WriteLine($"IMGViewer: detección de áreas no disponible: {ex.Message}");}
                };
                Selection? result=null;
                if(selector.ShowDialog()==System.Windows.Forms.DialogResult.OK && selector.Result is {} bitmap)
                {
                    bitmap.Freeze();result=new Selection(bitmap,selector.CaptureOrigin);
                }
                completion.TrySetResult(result);
            }
            catch(Exception ex){completion.TrySetException(ex);}
            finally {dispatcher.InvokeShutdown();}
        }) {IsBackground=true,Name="IMGViewer capture selection"};
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
