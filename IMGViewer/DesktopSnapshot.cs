using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMGViewer;

internal sealed class DesktopSnapshot(BitmapSource bitmap, Int32Rect bounds, IReadOnlyList<Int32Rect> windows)
{
    internal BitmapSource Bitmap { get; } = bitmap;
    internal Int32Rect Bounds { get; } = bounds;
    internal IReadOnlyList<Int32Rect> Windows { get; } = windows;

    internal static DesktopSnapshot Capture()
    {
        var desktop = new Int32Rect(GetSystemMetrics(76), GetSystemMetrics(77),
            GetSystemMetrics(78), GetSystemMetrics(79));
        var visibleWindows = new List<Int32Rect>();
        EnumWindows((window, _) =>
        {
            if (IsWindowVisible(window) && !IsIconic(window) && GetWindowTextLengthW(window) > 0 &&
                TryGetVisibleWindowRect(window, out NativeRect rect) &&
                (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) != 0 || cloaked == 0))
            {
                Int32Rect area = Intersect(rect.ToRect(), desktop);
                if (area.Width > 0 && area.Height > 0)
                {
                    visibleWindows.Add(area);
                }
            }

            return true;
        }, 0);

        return new DesktopSnapshot(CapturePixels(desktop), desktop, visibleWindows);
    }

    private static bool TryGetVisibleWindowRect(nint window, out NativeRect rect)
    {
        const int extendedFrameBounds = 9;
        if (DwmGetWindowAttribute(window, extendedFrameBounds, out rect, Marshal.SizeOf<NativeRect>()) == 0 &&
            rect.Right > rect.Left && rect.Bottom > rect.Top)
        {
            return true;
        }

        return GetWindowRect(window, out rect);
    }

    private static BitmapSource CapturePixels(Int32Rect desktop)
    {
        const uint copyIncludingLayeredWindows = 0x40CC0020;
        nint screen = GetDC(0);
        nint buffer = CreateCompatibleDC(screen);
        nint bitmap = CreateCompatibleBitmap(screen, desktop.Width, desktop.Height);
        nint previous = 0;
        try
        {
            if (screen == 0 || buffer == 0 || bitmap == 0)
            {
                throw new Win32Exception("No se pudo preparar la captura.");
            }

            previous = SelectObject(buffer, bitmap);
            if (previous == 0 || previous == -1 ||
                !BitBlt(buffer, 0, 0, desktop.Width, desktop.Height, screen, desktop.X, desktop.Y,
                    copyIncludingLayeredWindows))
            {
                throw new Win32Exception("No se pudo capturar el escritorio.");
            }

            BitmapSource pixels = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            var result = new FormatConvertedBitmap(pixels, PixelFormats.Bgr32, null, 0);
            result.Freeze();
            return result;
        }
        finally
        {
            if (previous != 0 && previous != -1) SelectObject(buffer, previous);
            if (bitmap != 0) DeleteObject(bitmap);
            if (buffer != 0) DeleteDC(buffer);
            if (screen != 0) ReleaseDC(0, screen);
        }
    }

    internal BitmapSource Crop(Int32Rect area)
    {
        area = Intersect(area, Bounds);
        var crop = new CroppedBitmap(Bitmap, new Int32Rect(area.X - Bounds.X, area.Y - Bounds.Y, area.Width, area.Height));
        var image = new WriteableBitmap(crop);
        image.Freeze();
        return image;
    }

    internal Int32Rect MonitorAt(Point point)
    {
        nint monitor = MonitorFromPoint(new NativePoint((int)point.X, (int)point.Y), 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfoW(monitor, ref info) ? Intersect(info.Monitor.ToRect(), Bounds) : Bounds;
    }

    internal static Point CursorPosition()
    {
        GetCursorPos(out NativePoint point);
        return new Point(point.X, point.Y);
    }

    internal static void PlaceOverlay(nint window, Int32Rect area) =>
        SetWindowPos(window, -1, area.X, area.Y, area.Width, area.Height, 0);

    private static Int32Rect Intersect(Int32Rect a, Int32Rect b)
    {
        int x = Math.Max(a.X, b.X);
        int y = Math.Max(a.Y, b.Y);
        return new Int32Rect(x, y,
            Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - x),
            Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - y));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly Int32Rect ToRect() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    private delegate bool EnumWindowsCallback(nint window, nint state);

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, nint state);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern int GetWindowTextLengthW(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint order, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out NativeRect value, int size);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint dest, int x, int y, int width, int height,
        nint source, int sourceX, int sourceY, uint operation);
}
