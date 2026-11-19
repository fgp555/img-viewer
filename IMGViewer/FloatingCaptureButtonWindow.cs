using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace IMGViewer;
internal sealed class FloatingCaptureButtonWindow:Window
{
    private const int Side=44;
    private readonly Action capture;
    private readonly Action<Point> remember;
    private readonly Button camera;
    private Point? press,windowOrigin;
    private Point? desired;
    private bool dragged,placing;
    internal FloatingCaptureButtonWindow(Action capture,Action open,Action hide,Point? savedPosition,Action<Point> remember,ResourceDictionary theme)
    {
        this.capture=capture;this.remember=remember;desired=savedPosition;
        Title="IMG Viewer · Captura flotante";Width=Height=Side;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        ShowActivated=false;ShowInTaskbar=false;Topmost=true;WindowStartupLocation=WindowStartupLocation.Manual;
        Background=new SolidColorBrush(Color.FromRgb(32,35,36));
        camera=new Button {Padding=new Thickness(0),Background=Background,Foreground=Brushes.White,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,
            ToolTip="Capturar pantalla · Arrastra para mover · Clic derecho para más opciones",
            Content=new TextBlock {Text="\uE722",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=20,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        camera.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
          <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
            <Border x:Name="Surface" Background="{TemplateBinding Background}" CornerRadius="8"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
            <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Background" Value="#383C3D"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Surface" Property="BorderBrush" Value="#A5CFFF"/><Setter TargetName="Surface" Property="BorderThickness" Value="1"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers>
          </ControlTemplate>
          """);
        System.Windows.Automation.AutomationProperties.SetName(camera,"Capturar pantalla");
        camera.PreviewMouseLeftButtonDown+=PointerDown;camera.PreviewMouseMove+=PointerMove;camera.PreviewMouseLeftButtonUp+=PointerUp;
        camera.LostMouseCapture+=(_,_)=>{press=null;windowOrigin=null;camera.Opacity=1;};
        camera.Click+=(_,_)=>QueueCapture();
        var menu=new ContextMenu();
        if(theme[typeof(ContextMenu)] is Style menuStyle)menu.Style=menuStyle;
        void Item(string title,Action action) {var item=new MenuItem {Header=title};if(theme[typeof(MenuItem)] is Style style)item.Style=style;item.Click+=(_,_)=>Dispatcher.InvokeAsync(action,DispatcherPriority.Background);menu.Items.Add(item);}
        Item("Capturar pantalla",capture);Item("Abrir IMG Viewer",open);
        var separator=new Separator();if(theme[typeof(Separator)] is Style separatorStyle)separator.Style=separatorStyle;menu.Items.Add(separator);
        Item("Ocultar botón",hide);camera.ContextMenu=menu;Content=camera;
        SourceInitialized+=(_,_)=>{
            nint handle=new WindowInteropHelper(this).Handle;
            SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x08000000|0x80);
            int dark=1,corners=2,border=0x003B3835;
            DwmSetWindowAttribute(handle,20,ref dark,4);DwmSetWindowAttribute(handle,33,ref corners,4);DwmSetWindowAttribute(handle,34,ref border,4);
        };
        Loaded+=(_,_)=>Place();
        DpiChanged+=(_,_)=>Dispatcher.InvokeAsync(()=>{if(press is null)Place();},DispatcherPriority.Loaded);
        SystemParameters.StaticPropertyChanged+=DisplaySettingsChanged;
        Closing+=(_,_)=>RememberPosition();
        Closed+=(_,_)=>SystemParameters.StaticPropertyChanged-=DisplaySettingsChanged;
    }
    private void DisplaySettingsChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName is nameof(SystemParameters.WorkArea) or nameof(SystemParameters.VirtualScreenWidth) or nameof(SystemParameters.VirtualScreenHeight))
            Dispatcher.InvokeAsync(()=>{if(press is null)Place();},DispatcherPriority.Loaded);
    }
    internal static Point ClampPosition(Rect work,Point point,double width,double height)=>new(
        Math.Clamp(point.X,work.Left,Math.Max(work.Left,work.Right-width)),
        Math.Clamp(point.Y,work.Top,Math.Max(work.Top,work.Bottom-height)));
    private void Place()
    {
        if(placing || !IsVisible)return;placing=true;
        try
        {
            var screen=desired is Point saved?Forms.Screen.FromPoint(new System.Drawing.Point((int)saved.X,(int)saved.Y)):Forms.Screen.PrimaryScreen??Forms.Screen.AllScreens[0];
            var work=screen.WorkingArea;var nativePoint=new NativePoint {X=work.Left+work.Width/2,Y=work.Top+work.Height/2};
            double scale=GetDpiForMonitor(MonitorFromPoint(nativePoint,2),0,out uint x,out _)==0?x/96.0:1;
            int size=(int)Math.Ceiling(Side*scale);
            var initial=desired??new Point(work.Right-size-16,work.Top+(work.Height-size)/2.0);
            desired=ClampPosition(new Rect(work.Left,work.Top,work.Width,work.Height),initial,size,size);
            SetWindowPos(new WindowInteropHelper(this).Handle,-1,(int)desired.Value.X,(int)desired.Value.Y,size,size,0x0010|0x0040);
        }
        finally {placing=false;}
    }
    private void PointerDown(object sender,MouseButtonEventArgs e)
    {
        if(!GetWindowRect(new WindowInteropHelper(this).Handle,out var rect))return;
        press=PointToScreen(e.GetPosition(this));windowOrigin=new Point(rect.Left,rect.Top);dragged=false;
        if(camera.CaptureMouse()){camera.Opacity=0.8;e.Handled=true;}else{press=null;windowOrigin=null;}
    }
    private void PointerMove(object sender,MouseEventArgs e)
    {
        if(press is not Point start||windowOrigin is not Point origin||!camera.IsMouseCaptured)return;
        var delta=PointToScreen(e.GetPosition(this))-start;var dpi=VisualTreeHelper.GetDpi(this);
        dragged|=Math.Abs(delta.X)>=SystemParameters.MinimumHorizontalDragDistance*dpi.DpiScaleX||Math.Abs(delta.Y)>=SystemParameters.MinimumVerticalDragDistance*dpi.DpiScaleY;
        if(!dragged)return;
        desired=origin+delta;
        SetWindowPos(new WindowInteropHelper(this).Handle,-1,(int)desired.Value.X,(int)desired.Value.Y,0,0,0x0001|0x0010);
        e.Handled=true;
    }
    private void PointerUp(object sender,MouseButtonEventArgs e)
    {
        if(press is null)return;bool wasDragged=dragged;camera.ReleaseMouseCapture();
        if(wasDragged){Place();RememberPosition();}else QueueCapture();
        e.Handled=true;
    }
    private void QueueCapture()=>Dispatcher.InvokeAsync(capture,DispatcherPriority.Background);
    private void RememberPosition()
    {
        if(!GetWindowRect(new WindowInteropHelper(this).Handle,out var rect))return;
        desired=new Point(rect.Left,rect.Top);
        try {remember(desired.Value);}
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException){camera.ToolTip="No se pudo recordar la posición. Puedes seguir capturando.";}
    }
    [StructLayout(LayoutKind.Sequential)]private struct NativePoint {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint handle,out NativeRect rect);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]private static extern int GetWindowLong(nint handle,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")]private static extern int SetWindowLong(nint handle,int index,int value);
    [DllImport("user32.dll")]private static extern nint MonitorFromPoint(NativePoint point,uint flags);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint handle,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("shcore.dll")]private static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(nint handle,int attribute,ref int value,int size);
}
