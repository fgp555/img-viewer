using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace IMGViewer;
internal sealed class CaptureThumbnailWindow : Window
{
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(200) };
    private readonly Stopwatch elapsed=Stopwatch.StartNew();
    private readonly Point captureOrigin;
    private readonly bool onPrimaryMonitor=CapturePreferences.ThumbnailOnPrimaryMonitor;
    private readonly int duration=CapturePreferences.ThumbnailSeconds;
    internal CaptureThumbnailWindow(BitmapSource bitmap,Func<string> save,Action open,string? error,Point captureOrigin)
    {
        this.captureOrigin=captureOrigin;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=false;ShowActivated=false;Topmost=true;Background=new SolidColorBrush(Color.FromRgb(32,35,36));
        // The preview and toolbar have independent layout: small/portrait captures keep
        // their aspect ratio without shrinking the action targets or cropping the bitmap.
        double scale=Math.Min(320.0/bitmap.PixelWidth,240.0/bitmap.PixelHeight);
        double previewWidth=bitmap.PixelWidth*scale,previewHeight=bitmap.PixelHeight*scale;
        Width=Math.Max(160,previewWidth);Height=40+previewHeight;
        var panel=new Grid();
        panel.RowDefinitions.Add(new RowDefinition {Height=new GridLength(40)});
        panel.RowDefinitions.Add(new RowDefinition {Height=new GridLength(previewHeight)});
        var actionStyle=(Style)System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
              <Setter Property="Padding" Value="0"/>
              <Setter Property="HorizontalContentAlignment" Value="Center"/>
              <Setter Property="VerticalContentAlignment" Value="Center"/>
              <Setter Property="Template">
                <Setter.Value><ControlTemplate TargetType="Button">
                  <Border x:Name="Surface" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Background="{TemplateBinding Background}" CornerRadius="4">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Background" Value="#383C3D"/></Trigger>
                    <Trigger Property="IsPressed" Value="True"><Setter TargetName="Surface" Property="Background" Value="#484C4D"/></Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Surface" Property="BorderBrush" Value="#B0B8BC"/><Setter TargetName="Surface" Property="BorderThickness" Value="1"/></Trigger>
                    <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value>
              </Setter>
            </Style>
            """);
        var imageButton=new Button {Style=actionStyle,Content=new Image {Source=bitmap,Stretch=Stretch.Uniform},Background=Background,BorderThickness=new Thickness(0),Padding=new Thickness(0),ToolTip="Abrir captura en IMG Viewer"};
        System.Windows.Automation.AutomationProperties.SetName(imageButton,"Abrir captura en IMG Viewer");
        imageButton.Click+=(_,_)=>{Close();open();};
        var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4)};
        void Add(string icon,string label,Action action)
        {
            var button=new Button {Style=actionStyle,Width=32,Height=32,Margin=new Thickness(2,0,2,0),Background=Brushes.Transparent,Foreground=Brushes.White,ToolTip=label,
                Content=new TextBlock {Text=icon,Width=20,Height=20,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=16,Foreground=Brushes.White}};
            System.Windows.Automation.AutomationProperties.SetName(button,label);
            button.Click+=(_,_)=>{
                try {action();}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ExternalException or InvalidOperationException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
                {
                    button.ToolTip="No se pudo completar la acción. Inténtalo de nuevo.";
                    var feedback=new ToolTip {Content=button.ToolTip,PlacementTarget=button,IsOpen=true};
                    var dismiss=new DispatcherTimer {Interval=TimeSpan.FromSeconds(5)};
                    dismiss.Tick+=(_,_)=>{feedback.IsOpen=false;dismiss.Stop();};dismiss.Start();
                }
                elapsed.Restart();if(IsMouseOver)elapsed.Stop();
            };
            actions.Children.Add(button);
        }
        Add("\uE8C8","Copiar imagen",()=>Clipboard.SetImage(bitmap));
        Add("\uE71B","Guardar y copiar ruta local",()=>Clipboard.SetText(save()));
        Add("\uE74E","Guardar captura",()=>save());
        Add("\uE711","Cerrar miniatura (conservar historial)",Close);
        var menu=new ContextMenu();
        var folderItem=new MenuItem {Header="Guardar y abrir carpeta"};
        folderItem.Click+=(_,_)=>{
            try {string path=save();var start=new ProcessStartInfo("explorer.exe") {UseShellExecute=true};start.ArgumentList.Add("/select,"+path);Process.Start(start);}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception) {folderItem.ToolTip="No se pudo abrir la carpeta.";}
        };
        menu.Items.Add(folderItem);imageButton.ContextMenu=menu;
        menu.Opened+=(_,_)=>elapsed.Stop();menu.Closed+=(_,_)=>{if(!IsMouseOver)elapsed.Start();};
        var toolbar=new Border {BorderBrush=new SolidColorBrush(Color.FromRgb(52,56,57)),BorderThickness=new Thickness(0,0,0,1),Child=actions};
        panel.Children.Add(toolbar);Grid.SetRow(imageButton,1);panel.Children.Add(imageButton);Content=panel;
        if(error is not null) imageButton.ToolTip=error;
        SourceInitialized+=(_,_)=>{
            nint hwnd=new WindowInteropHelper(this).Handle;
            SetWindowLongW(hwnd,-20,GetWindowLongW(hwnd,-20)|0x08000000|0x80);
            int dark=1,corners=2;DwmSetWindowAttribute(hwnd,20,ref dark,4);DwmSetWindowAttribute(hwnd,33,ref corners,4);
        };
        Loaded+=(_,_)=>PlaceAtPreferredMonitor();
        ContentRendered+=(_,_)=>{PlaceAtPreferredMonitor();elapsed.Restart();};
        DpiChanged+=(_,_)=>Dispatcher.InvokeAsync(PlaceAtPreferredMonitor,DispatcherPriority.Loaded);
        MouseEnter+=(_,_)=>elapsed.Stop();MouseLeave+=(_,_)=>elapsed.Start();
        timer.Tick+=(_,_)=>{if(elapsed.Elapsed.TotalSeconds>=duration)Close();};timer.Start();
        Closed+=(_,_)=>timer.Stop();
    }
    private void PlaceAtPreferredMonitor()
    {
        var point=new NativePoint {X=(int)captureOrigin.X,Y=(int)captureOrigin.Y};
        var info=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>()};
        // Windows places the primary monitor at desktop origin (0,0).
        // The alternative follows the selected region, including negative monitor origins.
        nint monitor=onPrimaryMonitor ? MonitorFromPoint(default,1) : MonitorFromPoint(point,2);
        if(!GetMonitorInfo(monitor,ref info))return;
        uint dpiX=96,dpiY=96;
        if(GetDpiForMonitor(monitor,0,out uint x,out uint y)==0) {dpiX=x;dpiY=y;}
        int width=Math.Min(info.Work.Right-info.Work.Left, (int)Math.Ceiling(Width*dpiX/96.0));
        int height=Math.Min(info.Work.Bottom-info.Work.Top,(int)Math.Ceiling(Height*dpiY/96.0));
        int left=Math.Max(info.Work.Left,info.Work.Right-width-16);
        int top=Math.Max(info.Work.Top,info.Work.Bottom-height-16);
        // Use physical coordinates and the target monitor DPI, including negative desktop origins.
        SetWindowPos(new WindowInteropHelper(this).Handle,-1,left,top,width,height,0x0010|0x0040);
    }
    [DllImport("shcore.dll")]private static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public int Flags;}
    [DllImport("user32.dll")]private static extern nint MonitorFromPoint(NativePoint point,uint flags);
    [DllImport("user32.dll",EntryPoint="GetMonitorInfoW")]private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]private static extern int GetWindowLongW(nint hwnd,int index);
    [DllImport("user32.dll")]private static extern int SetWindowLongW(nint hwnd,int index,int value);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
}
