using System.Runtime.InteropServices;

namespace IMGViewer;

public partial class MainWindow
{
    private const int AlwaysOnTopHotkey = 2;

    private void ToggleForegroundTopmost(nint viewerHandle)
    {
        nint foreground = GetForegroundWindow();
        if (foreground == 0 || foreground == GetDesktopWindow() || foreground == GetShellWindow())
        {
            return;
        }

        var viewer=System.Windows.Application.Current.Windows.OfType<MainWindow>()
            .FirstOrDefault(window=>new System.Windows.Interop.WindowInteropHelper(window).Handle==foreground);
        if(viewer is not null){viewer.ToggleViewerTopmost();return;}
        if (!ToggleWindowTopmost(foreground))
            StatusText.Text = "No se pudo cambiar Siempre visible en la ventana activa.";
        else
        {
            bool pinned=IsWindowTopmost(foreground);
            ExternalTopmostPins.Track(foreground,pinned);
            NotifyExternalTopmost(pinned,foreground);
        }
    }
    internal static void NotifyExternalTopmost(bool pinned,nint target)
    {
        var title=new System.Text.StringBuilder(256);GetWindowTextW(target,title,title.Capacity);
        ShowTopmostNotice(pinned,title.ToString());
    }
    private static bool IsWindowTopmost(nint window)=>(GetWindowLongW(window,-20)&8)!=0;
    private bool ViewerPinned=>windowedMode is {} previous?previous.WasTopmost:Topmost;
    private void ToggleViewerTopmost()
    {
        bool pinned=!ViewerPinned;
        if(windowedMode is {} previous){previous.WasTopmost=pinned;windowedMode=previous;}
        else Topmost=pinned;
        UpdateTopmostIndicator();ShowTopmostNotice(pinned,"IMG Viewer");
    }
    private void TopmostPin_Click(object sender,System.Windows.RoutedEventArgs e)=>ToggleViewerTopmost();
    private void UpdateTopmostIndicator()
    {
        if(TopmostPin is null)return;
        TopmostPin.Visibility=ViewerPinned?System.Windows.Visibility.Visible:System.Windows.Visibility.Collapsed;
        ZenTopmostZone.Visibility=ViewerPinned&&zenMode&&!windowedMode.HasValue?System.Windows.Visibility.Visible:System.Windows.Visibility.Collapsed;
    }
    private static System.Windows.Window? topmostNotice;
    private static System.Windows.Threading.DispatcherTimer? topmostNoticeTimer;
    private static void ShowTopmostNotice(bool pinned,string title)
    {
        topmostNoticeTimer?.Stop();topmostNotice?.Close();
        var area=System.Windows.SystemParameters.WorkArea;
        var notice=new System.Windows.Window {Width=340,Height=58,Left=area.Right-360,Top=area.Bottom-78,
            WindowStyle=System.Windows.WindowStyle.None,ResizeMode=System.Windows.ResizeMode.NoResize,
            ShowActivated=false,ShowInTaskbar=false,Topmost=true,Background=System.Windows.Media.Brushes.Transparent,AllowsTransparency=true};
        notice.Content=new System.Windows.Controls.Border {Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32,35,36)),
            BorderBrush=System.Windows.Media.Brushes.DimGray,BorderThickness=new System.Windows.Thickness(1),CornerRadius=new System.Windows.CornerRadius(8),Padding=new System.Windows.Thickness(14,8,14,8),
            Child=new System.Windows.Controls.TextBlock {Text=$"{(pinned?"Siempre visible activado":"Siempre visible desactivado")}\n{title}",Foreground=System.Windows.Media.Brushes.White,TextTrimming=System.Windows.TextTrimming.CharacterEllipsis,FontSize=12}};
        topmostNotice=notice;notice.Show();
        topmostNoticeTimer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromSeconds(2)};
        topmostNoticeTimer.Tick+=(_,_)=>{topmostNoticeTimer?.Stop();topmostNotice?.Close();topmostNotice=null;};topmostNoticeTimer.Start();
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetWindowTextW(nint window,System.Text.StringBuilder text,int count);

    private static bool ToggleWindowTopmost(nint window)
    {
        const int extendedStyle = -20;
        const int topmostStyle = 0x00000008;
        const uint keepSizePositionAndFocus = 0x0001 | 0x0002 | 0x0010;
        bool isTopmost = (GetWindowLongW(window, extendedStyle) & topmostStyle) != 0;
        nint order = isTopmost ? -2 : -1;
        return SetWindowPos(window, order, 0, 0, 0, 0, keepSizePositionAndFocus);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetDesktopWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetShellWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetWindowLongW(nint window, int index);
}
