using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace IMGViewer;
internal sealed class GalleryDragPreview : IDisposable
{
    private readonly Window window;
    private readonly nint handle;
    internal GalleryDragPreview(BitmapSource? image, string name)
    {
        var panel=new StackPanel();
        if(image is not null) panel.Children.Add(new Image { Source=image, Height=72, Stretch=Stretch.Uniform });
        panel.Children.Add(new TextBlock { Text=name, Foreground=Brushes.White, FontSize=11, Margin=new Thickness(0,6,0,0), TextTrimming=TextTrimming.CharacterEllipsis });
        window=new Window { WindowStyle=WindowStyle.None, ResizeMode=ResizeMode.NoResize, AllowsTransparency=true,
            Background=Brushes.Transparent, ShowInTaskbar=false, ShowActivated=false, Topmost=true,
            Width=144, SizeToContent=SizeToContent.Height, IsHitTestVisible=false, Focusable=false, Opacity=0.85,
            Left=-10000, Top=-10000, Content=new Border { Background=new SolidColorBrush(Color.FromRgb(32,35,36)), CornerRadius=new CornerRadius(8), Padding=new Thickness(8), Child=panel } };
        handle=new WindowInteropHelper(window).EnsureHandle();
        // Click-through, tool window, never activate: the target receives the actual drop.
        SetWindowLongW(handle,-20,GetWindowLongW(handle,-20)|0x20|0x80|0x08000000);
        window.Show(); FollowCursor();
    }
    internal void FollowCursor()
    {
        var cursor=DesktopSnapshot.CursorPosition();
        SetWindowPos(handle, -1, (int)cursor.X+18, (int)cursor.Y+20, 0,0,0x0011);
    }
    public void Dispose() => window.Close();
    [DllImport("user32.dll")] private static extern int GetWindowLongW(nint window,int index);
    [DllImport("user32.dll")] private static extern int SetWindowLongW(nint window,int index,int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
}
