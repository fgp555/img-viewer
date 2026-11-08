using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace IMGViewer;

internal sealed class IdleCursor(HwndSource source) : IDisposable
{
    private const int RawInputMessage = 0x00FF;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private long lastActivity;
    private int delayMilliseconds;
    private bool hidden;
    private bool systemWide;
    private Point? previousPosition;

    internal bool Configure(bool enabled, int seconds, bool acrossWindows)
    {
        Stop();
        delayMilliseconds = seconds * 1000;
        if (!enabled)
        {
            return true;
        }

        systemWide = acrossWindows && StartGlobalInput();
        Reset();
        if (!systemWide)
        {
            InputManager.Current.PostProcessInput += ObserveInput;
        }

        timer.Tick += CheckInactivity;
        timer.Start();
        return !acrossWindows || systemWide;
    }

    private bool StartGlobalInput()
    {
        if (!MagInitialize())
        {
            return false;
        }

        const uint inputSink = 0x00000100;
        var device = new RawInputDevice { UsagePage = 1, Usage = 2, Flags = inputSink, Target = source.Handle };
        if (!RegisterMouse(ref device))
        {
            MagUninitialize();
            return false;
        }

        source.AddHook(ObserveRawInput);
        previousPosition = null;
        return true;
    }

    private nint ObserveRawInput(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == RawInputMessage)
        {
            Reset();
        }

        return 0;
    }

    private void ObserveInput(object sender, ProcessInputEventArgs e)
    {
        RoutedEvent input = e.StagingItem.Input.RoutedEvent;
        if (input == Mouse.PreviewMouseMoveEvent || input == Mouse.PreviewMouseDownEvent ||
            input == Mouse.PreviewMouseUpEvent || input == Mouse.PreviewMouseWheelEvent)
        {
            Reset();
        }
    }

    private void CheckInactivity(object? sender, EventArgs e)
    {
        if (systemWide)
        {
            if (!GetCursorPos(out NativePoint cursor))
            {
                previousPosition = null;
                Reset();
                return;
            }

            var position = new Point(cursor.X, cursor.Y);
            if (previousPosition != position || GetAsyncKeyState(1) < 0 || GetAsyncKeyState(2) < 0 ||
                GetAsyncKeyState(4) < 0 || GetAsyncKeyState(5) < 0 || GetAsyncKeyState(6) < 0)
            {
                previousPosition = position;
                Reset();
                return;
            }
        }
        else if (Mouse.Captured is not null || Mouse.DirectlyOver is not DependencyObject element ||
                 Window.GetWindow(element) is not { IsActive: true, IsMouseOver: true })
        {
            Reset();
            return;
        }

        if (!hidden && Environment.TickCount64 - lastActivity >= delayMilliseconds)
        {
            if (systemWide)
            {
                hidden = MagShowSystemCursor(false);
            }
            else
            {
                Mouse.OverrideCursor = Cursors.None;
                hidden = true;
            }
        }
    }

    internal void ResetLocal()
    {
        if (!systemWide)
        {
            Reset();
        }
    }

    private void Reset()
    {
        lastActivity = Environment.TickCount64;
        if (!hidden)
        {
            return;
        }

        if (systemWide)
        {
            hidden = !MagShowSystemCursor(true);
        }
        else
        {
            Mouse.OverrideCursor = null;
            hidden = false;
        }
    }

    private void Stop()
    {
        Reset();
        if (!timer.IsEnabled)
        {
            return;
        }

        timer.Stop();
        timer.Tick -= CheckInactivity;
        if (systemWide)
        {
            const uint remove = 1;
            var device = new RawInputDevice { UsagePage = 1, Usage = 2, Flags = remove };
            RegisterMouse(ref device);
            if (!source.IsDisposed)
            {
                source.RemoveHook(ObserveRawInput);
            }

            MagUninitialize();
            systemWide = false;
            hidden = false;
        }
        else
        {
            InputManager.Current.PostProcessInput -= ObserveInput;
        }
    }

    public void Dispose() => Stop();

    private static bool RegisterMouse(ref RawInputDevice device) =>
        RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<RawInputDevice>());

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagUninitialize();

    [DllImport("Magnification.dll", ExactSpelling = true)]
    private static extern bool MagShowSystemCursor(bool show);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool RegisterRawInputDevices(ref RawInputDevice devices, uint count, uint size);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern short GetAsyncKeyState(int key);
}
