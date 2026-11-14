using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private const int SizingMessage = 0x0214;
    private const int ControlKey = 0x11;

    private enum SizingEdge
    {
        Left = 1,
        Right,
        Top,
        TopLeft,
        TopRight,
        Bottom,
        BottomLeft,
        BottomRight
    }

    private void SetPhotoPixelScale(double factor)
    {
        if (Photo.Source is not BitmapSource bitmap)
        {
            return;
        }

        StopGridPhotoAnimation();
        coverPhoto = false;
        var dpi = VisualTreeHelper.GetDpi(Photo);
        if (zenMode)
        {
            if (windowedMode.HasValue)
            {
                ToggleFullScreen();
            }
    
            WindowState = WindowState.Normal;
            dpi = VisualTreeHelper.GetDpi(this);
            UpdateLayout();
            double chromeWidth = Math.Max(0, ActualWidth - PhotoViewport.ActualWidth) * dpi.DpiScaleX;
            double chromeHeight = Math.Max(0, ActualHeight - PhotoViewport.ActualHeight) * dpi.DpiScaleY;
            double ratio = (double)bitmap.PixelWidth / bitmap.PixelHeight;
            double height = Math.Max(bitmap.PixelHeight * factor,
                Math.Max(Math.Max(1, MinHeight * dpi.DpiScaleY - chromeHeight), Math.Max(1, MinWidth * dpi.DpiScaleX - chromeWidth) / ratio));
            int pixelHeight = (int)Math.Ceiling(height + chromeHeight);
            int pixelWidth = (int)Math.Ceiling(height * ratio + chromeWidth);
            nint handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            const uint keepPositionOrderAndFocus = 0x0002 | 0x0004 | 0x0010;
            SetWindowPos(handle, 0, 0, 0, pixelWidth, pixelHeight, keepPositionOrderAndFocus);
    
        }

        PhotoViewport.UpdateLayout();
        dpi = VisualTreeHelper.GetDpi(Photo);
        if (Photo.ActualWidth > 0)
        {
            photoZoomMultiplier = bitmap.PixelWidth * factor / (Photo.ActualWidth * dpi.DpiScaleX);
            UpdatePhotoZoom();
            SetPhotoPan(0, 0);
        }
    }

    private bool TryResizeToPhoto(nint edge, nint bounds)
    {
        if (bounds == 0 || GetKeyState(ControlKey) >= 0 || IsFolderGridOpen ||
            windowedMode.HasValue || WindowState != WindowState.Normal || Photo.Source is not BitmapSource photo)
        {
            return false;
        }

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        var rect = Marshal.PtrToStructure<ScreenRect>(bounds);
        var size = new Size(Math.Ceiling(MinWidth * dpi.DpiScaleX), Math.Ceiling(MinHeight * dpi.DpiScaleY));
        double ratio = (double)photo.PixelWidth / photo.PixelHeight;
        var chrome = new Size(Math.Max(0, ActualWidth - PhotoViewport.ActualWidth) * dpi.DpiScaleX,
            Math.Max(0, ActualHeight - PhotoViewport.ActualHeight) * dpi.DpiScaleY);
        rect = ConstrainResize(rect, (SizingEdge)(int)edge, ratio, size, chrome);
        Marshal.StructureToPtr(rect, bounds, false);
        return true;
    }

    private static ScreenRect ConstrainResize(ScreenRect rect, SizingEdge edge, double ratio, Size minimum, Size chrome)
    {
        double fullWidth = rect.Right - rect.Left;
        double fullHeight = rect.Bottom - rect.Top;
        double width = Math.Max(1, fullWidth - chrome.Width);
        double height = Math.Max(1, fullHeight - chrome.Height);
        double desiredHeight = edge switch
        {
            SizingEdge.Left or SizingEdge.Right => width / ratio,
            SizingEdge.Top or SizingEdge.Bottom => height,
            // Project the dragged corner onto the line defined by the photo's aspect ratio.
            _ => (width * ratio + height) / (ratio * ratio + 1)
        };

        desiredHeight = Math.Max(desiredHeight, Math.Max(Math.Max(1, minimum.Height - chrome.Height), Math.Max(1, minimum.Width - chrome.Width) / ratio));
        int targetWidth = (int)Math.Round(desiredHeight * ratio + chrome.Width);
        int targetHeight = (int)Math.Round(desiredHeight + chrome.Height);

        if (edge is SizingEdge.Top or SizingEdge.Bottom)
        {
            rect.Left -= (targetWidth - (int)fullWidth) / 2;
            rect.Right = rect.Left + targetWidth;
        }
        else if (edge is SizingEdge.Left or SizingEdge.TopLeft or SizingEdge.BottomLeft)
        {
            rect.Left = rect.Right - targetWidth;
        }
        else
        {
            rect.Right = rect.Left + targetWidth;
        }

        if (edge is SizingEdge.Left or SizingEdge.Right)
        {
            rect.Top -= (targetHeight - (int)fullHeight) / 2;
            rect.Bottom = rect.Top + targetHeight;
        }
        else if (edge is SizingEdge.Top or SizingEdge.TopLeft or SizingEdge.TopRight)
        {
            rect.Top = rect.Bottom - targetHeight;
        }
        else
        {
            rect.Bottom = rect.Top + targetHeight;
        }

        return rect;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern short GetKeyState(int key);
}
