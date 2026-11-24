using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace IMGViewer;

// One event-driven overlay per explicitly pinned external window; no polling timer.
internal static class ExternalTopmostPins
{
    private static readonly Dictionary<nint,Window> pins=[];
    private static readonly List<nint> hooks=[];
    private static readonly WinEventProc callback=OnEvent;
    private static bool queued;
    internal static void Track(nint target,bool pinned)
    {
        if(!pinned){Remove(target);return;}
        if(pins.ContainsKey(target)){Refresh();return;}
        var pin=new Window {Title="IMG Viewer · Siempre visible",Width=32,Height=32,WindowStyle=WindowStyle.None,
            ResizeMode=ResizeMode.NoResize,ShowActivated=false,ShowInTaskbar=false,Topmost=true,
            Background=Brushes.Transparent,AllowsTransparency=true};
        var button=new Button {Width=32,Height=32,Cursor=System.Windows.Input.Cursors.Hand,
            Background=Brushes.Transparent,Foreground=Brushes.White,BorderThickness=new Thickness(0),
            ToolTip="Siempre visible · Clic para desactivar · Ctrl + F12",
            Content=new TextBlock {Text="\uE718",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=16,
                HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        if(Application.Current.MainWindow is MainWindow main)button.Style=(Style)main.FindResource("FloatingButton");
        System.Windows.Automation.AutomationProperties.SetName(button,"Desactivar siempre visible");
        button.Click+=(_,_)=>{
            if(IsWindow(target)&&SetWindowPos(target,-2,0,0,0,0,0x13))
            {Remove(target);MainWindow.NotifyExternalTopmost(false,target);}
        };
        pin.Content=new Border {CornerRadius=new CornerRadius(6),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Child=button};
        pin.SourceInitialized+=(_,_)=>{
            nint handle=new WindowInteropHelper(pin).Handle;
            SetWindowLongW(handle,-20,GetWindowLongW(handle,-20)|0x08000000|0x80);
        };
        pins.Add(target,pin);
        if(hooks.Count==0)
        {
            Hook(3,3); // foreground
            Hook(0x16,0x17); // minimize start/end
            Hook(0x8001,0x8003); // destroy/show/hide
            Hook(0x800B,0x800B); // location change
        }
        new WindowInteropHelper(pin).EnsureHandle();Refresh();
    }
    private static void Hook(uint first,uint last)
    {
        nint hook=SetWinEventHook(first,last,0,callback,0,0,0);
        if(hook!=0)hooks.Add(hook);
    }
    private static void OnEvent(nint hook,uint kind,nint target,int objectId,int child,uint thread,uint time)
    {
        if(kind>=0x8000&&(objectId!=0||!pins.ContainsKey(target)))return;
        if(queued||Application.Current.Dispatcher.HasShutdownStarted)return;
        queued=true;
        Application.Current.Dispatcher.InvokeAsync(()=>{queued=false;Refresh();},DispatcherPriority.Render);
    }
    private static void Refresh()
    {
        nint active=GetForegroundWindow();
        foreach(var pair in pins.ToArray())
        {
            nint target=pair.Key;var pin=pair.Value;
            if(!IsWindow(target)||(GetWindowLongW(target,-20)&8)==0){Remove(target);continue;}
            if(!IsWindowVisible(target)||IsIconic(target)||active!=target){pin.Hide();continue;}
            if(!GetWindowRect(target,out var rect))continue;
            double scale=Math.Max(96,GetDpiForWindow(target))/96.0;
            // Leave the caption controls at the far right unobstructed.
            int size=(int)Math.Round(34*scale),gap=(int)Math.Round(150*scale);
            int x=Math.Max(rect.Left,rect.Right-gap-size),y=rect.Top+(int)Math.Round(5*scale);
            var handle=new WindowInteropHelper(pin).Handle;
            if(!pin.IsVisible)pin.Show();
            SetWindowPos(handle,-1,x,y,size,size,0x10|0x40);
        }
    }
    private static void Remove(nint target)
    {
        if(pins.Remove(target,out var pin))pin.Close();
        if(pins.Count==0){foreach(var hook in hooks)UnhookWinEvent(hook);hooks.Clear();}
    }
    internal static void Dispose()
    {
        foreach(var target in pins.Keys.ToArray())Remove(target);
    }
    private delegate void WinEventProc(nint hook,uint kind,nint window,int objectId,int child,uint thread,uint time);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min,uint max,nint module,WinEventProc callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window,out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowLongW(nint window,int index);
    [DllImport("user32.dll")] private static extern int SetWindowLongW(nint window,int index,int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window,nint order,int x,int y,int width,int height,uint flags);
}
