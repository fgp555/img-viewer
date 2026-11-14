using Microsoft.Win32;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace IMGViewer;
public partial class MainWindow
{
    private bool restoredStartupPlacement;
    private WindowState lastVisibleWindowState = WindowState.Normal;
    private void RestoreStartupPlacement()
    {
        if (restoredStartupPlacement) return;
        restoredStartupPlacement = true;
        using var settings = Registry.CurrentUser.OpenSubKey(@"Software\IMGViewer\WindowPlacement");
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        string? device = settings?.GetValue("Monitor") as string;
        bool monitorFound = Forms.Screen.AllScreens.Any(item => item.DeviceName == device);
        if (monitorFound) screen = Forms.Screen.AllScreens.First(item => item.DeviceName == device);
        var area = screen.WorkingArea;
        double scale = MonitorScale(area.Left + area.Width / 2, area.Top + area.Height / 2);
        double Read(string name, double fallback) => double.TryParse(settings?.GetValue(name) as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? value : fallback;
        bool saved = settings?.GetValue("Width") is string;
        int width = (int)Math.Round(saved ? Read("Width", area.Width * 0.6 / scale) * scale : area.Width * 0.6);
        int height = (int)Math.Round(saved ? Read("Height", area.Height * 0.6 / scale) * scale : area.Height * 0.6);
        width = Math.Clamp(width, Math.Min(area.Width,(int)Math.Ceiling(MinWidth*scale)),area.Width);
        height = Math.Clamp(height, Math.Min(area.Height,(int)Math.Ceiling(MinHeight*scale)),area.Height);
        int x = saved && monitorFound ? area.Left + (int)Math.Round(Read("OffsetX",0)*scale) : area.Left+(area.Width-width)/2;
        int y = saved && monitorFound ? area.Top + (int)Math.Round(Read("OffsetY",0)*scale) : area.Top+(area.Height-height)/2;
        x = Math.Clamp(x,area.Left,area.Right-width); y=Math.Clamp(y,area.Top,area.Bottom-height);
        WindowStartupLocation = WindowStartupLocation.Manual;
        SetWindowPos(new WindowInteropHelper(this).Handle,0,x,y,width,height,0x0004|0x0010);
        if (saved && settings?.GetValue("Maximized") is 1) WindowState = WindowState.Maximized;
        lastVisibleWindowState = WindowState;
        StateChanged += (_,_) => { if (WindowState != WindowState.Minimized) lastVisibleWindowState = WindowState; };
    }
    private void SaveStartupPlacement()
    {
        if (!restoredStartupPlacement) return;
        Rect bounds;
        WindowState state;
        if (zenMode && normalPlacement is { } normal) { bounds=normal.Bounds; state=normal.State; }
        else if (windowedMode is { } fullscreen)
        {
            var dpi=VisualTreeHelper.GetDpi(this);
            bounds=new Rect(fullscreen.Bounds.Left/dpi.DpiScaleX,fullscreen.Bounds.Top/dpi.DpiScaleY,
                (fullscreen.Bounds.Right-fullscreen.Bounds.Left)/dpi.DpiScaleX,(fullscreen.Bounds.Bottom-fullscreen.Bounds.Top)/dpi.DpiScaleY);
            state=fullscreen.State;
        }
        else { state=WindowState==WindowState.Minimized ? lastVisibleWindowState : WindowState; bounds=state==WindowState.Normal && WindowState==WindowState.Normal ? new Rect(Left,Top,ActualWidth,ActualHeight) : RestoreBounds; }
        if (bounds.IsEmpty || !double.IsFinite(bounds.Left) || bounds.Width<=0) return;
        var currentDpi=VisualTreeHelper.GetDpi(this);
        var center=new System.Drawing.Point((int)((bounds.Left+bounds.Width/2)*currentDpi.DpiScaleX),(int)((bounds.Top+bounds.Height/2)*currentDpi.DpiScaleY));
        var screen=Forms.Screen.FromPoint(center); var area=screen.WorkingArea;
        try
        {
            using var settings=Registry.CurrentUser.CreateSubKey(@"Software\IMGViewer\WindowPlacement");
            settings.SetValue("Monitor",screen.DeviceName);
            settings.SetValue("Width",bounds.Width.ToString(CultureInfo.InvariantCulture));
            settings.SetValue("Height",bounds.Height.ToString(CultureInfo.InvariantCulture));
            settings.SetValue("OffsetX",((bounds.Left*currentDpi.DpiScaleX-area.Left)/currentDpi.DpiScaleX).ToString(CultureInfo.InvariantCulture));
            settings.SetValue("OffsetY",((bounds.Top*currentDpi.DpiScaleY-area.Top)/currentDpi.DpiScaleY).ToString(CultureInfo.InvariantCulture));
            settings.SetValue("Maximized",state==WindowState.Maximized?1:0);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }
    private static double MonitorScale(int x,int y)
    {
        nint monitor=PlacementMonitorFromPoint(new PlacementPoint { X=x,Y=y },2);
        return GetDpiForMonitor(monitor,0,out uint dpi,out _) == 0 ? dpi/96.0 : 1;
    }
    [StructLayout(LayoutKind.Sequential)] private struct PlacementPoint { public int X,Y; }
    [DllImport("user32.dll",EntryPoint="MonitorFromPoint")] private static extern nint PlacementMonitorFromPoint(PlacementPoint point,uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);
}
