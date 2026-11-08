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
        rect = ConstrainResize(rect, (SizingEdge)(int)edge, ratio, size);
        Marshal.StructureToPtr(rect, bounds, false);
        return true;
    }

    private static ScreenRect ConstrainResize(ScreenRect rect, SizingEdge edge, double ratio, Size minimum)
    {
        double width = rect.Right - rect.Left;
        double height = rect.Bottom - rect.Top;
        double desiredHeight = edge switch
        {
            SizingEdge.Left or SizingEdge.Right => width / ratio,
            SizingEdge.Top or SizingEdge.Bottom => height,
            // Project the dragged corner onto the line defined by the photo's aspect ratio.
            _ => (width * ratio + height) / (ratio * ratio + 1)
        };

        desiredHeight = Math.Max(desiredHeight, Math.Max(minimum.Height, minimum.Width / ratio));
        int targetWidth = (int)Math.Round(desiredHeight * ratio);
        int targetHeight = (int)Math.Round(desiredHeight);

        if (edge is SizingEdge.Top or SizingEdge.Bottom)
        {
            rect.Left -= (targetWidth - (int)width) / 2;
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
            rect.Top -= (targetHeight - (int)height) / 2;
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
