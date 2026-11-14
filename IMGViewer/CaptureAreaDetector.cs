using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace IMGViewer;

// Read the original applications on an MTA worker before the frozen overlay exists.
internal static class CaptureAreaDetector
{
    private static Task<IReadOnlyDictionary<int, IReadOnlyList<Int32Rect>>>? pending;

    internal static async Task<IReadOnlyDictionary<int, IReadOnlyList<Int32Rect>>> DetectAsync(DesktopSnapshot snapshot)
    {
        // A broken accessibility provider must not block capture or accumulate workers.
        if (pending is { IsCompleted: false }) return new Dictionary<int, IReadOnlyList<Int32Rect>>();
        var collected = new ConcurrentDictionary<int, IReadOnlyList<Int32Rect>>();
        var task = Task.Run(() => Collect(snapshot, collected));
        pending = task;
        return await Task.WhenAny(task, Task.Delay(700)) == task
            ? await task : collected.ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<Int32Rect>> Collect(DesktopSnapshot snapshot, ConcurrentDictionary<int, IReadOnlyList<Int32Rect>> result)
    {
        var timer = Stopwatch.StartNew();
        var walker = TreeWalker.ControlViewWalker;
        Point cursor = DesktopSnapshot.CursorPosition();
        var order = Enumerable.Range(0, snapshot.Handles.Count).OrderByDescending(index =>
            new Rect(snapshot.Windows[index].X, snapshot.Windows[index].Y,
                snapshot.Windows[index].Width, snapshot.Windows[index].Height).Contains(cursor));
        foreach (int i in order)
        {
            if (timer.ElapsedMilliseconds >= 600) break;
            var areas = new HashSet<Int32Rect>();
            try
            {
                var root = AutomationElement.FromHandle(snapshot.Handles[i]);
                if (root.Current.ProcessId == Environment.ProcessId) continue;
                var queue = new Queue<(AutomationElement Element, int Depth)>();
                queue.Enqueue((root, 0));
                int visited = 0;
                while (queue.Count > 0 && visited++ < 800 && timer.ElapsedMilliseconds < 600)
                {
                    var (element, depth) = queue.Dequeue();
                    var info = element.Current;
                    if (info.IsOffscreen) continue;
                    var type = info.ControlType;
                    if (type == ControlType.Pane || type == ControlType.Document ||
                        type == ControlType.Group || type == ControlType.List ||
                        type == ControlType.DataGrid || type == ControlType.Tree ||
                        type == ControlType.Tab || type == ControlType.ToolBar)
                    {
                        Rect rect = info.BoundingRectangle;
                        rect.Intersect(new Rect(snapshot.Windows[i].X, snapshot.Windows[i].Y,
                            snapshot.Windows[i].Width, snapshot.Windows[i].Height));
                        if (!rect.IsEmpty && rect.Width >= 60 && rect.Height >= 40 &&
                            double.IsFinite(rect.X) && double.IsFinite(rect.Y))
                        {
                            int x = (int)Math.Ceiling(rect.Left), y = (int)Math.Ceiling(rect.Top);
                            var area = new Int32Rect(x, y, Math.Max(0, (int)Math.Floor(rect.Right) - x),
                                Math.Max(0, (int)Math.Floor(rect.Bottom) - y));
                            if (area.Width >= 60 && area.Height >= 40 && areas.Add(area))
                                result[i] = areas.ToArray();
                        }
                    }
                    if (depth >= 12) continue;
                    for (var child = walker.GetFirstChild(element); child is not null &&
                         queue.Count < 800 && timer.ElapsedMilliseconds < 600;
                         child = walker.GetNextSibling(child))
                        queue.Enqueue((child, depth + 1));
                }
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or
                                       COMException or UnauthorizedAccessException or ArgumentException)
            {
                // Keep any collected areas; window/manual selection remains available.
            }
            result[i] = areas.ToArray();
        }
        return result;
    }
}
