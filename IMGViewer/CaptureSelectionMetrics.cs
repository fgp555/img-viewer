using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace IMGViewer;

internal sealed class CaptureSelectionMetrics
{
    private readonly string engine;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly TimeSpan initialCpu;
    private readonly long initialMemory;
    private readonly long initialAllocated=GC.GetTotalAllocatedBytes();
    private double previousDragSample;
    private int dragSamples,slowSamples;
    private double maxDragGap;
    internal CaptureSelectionMetrics(string engine="wpf")
    {
        this.engine=engine;
        using var process=Process.GetCurrentProcess();
        initialCpu=process.TotalProcessorTime;initialMemory=process.WorkingSet64;
    }
    internal void StartDrag()=>previousDragSample=0;
    internal void Sample(bool dragging)
    {
        if(!dragging){previousDragSample=0;return;}
        double now=clock.Elapsed.TotalMilliseconds;
        if(previousDragSample>0)
        {
            double gap=now-previousDragSample;maxDragGap=Math.Max(maxDragGap,gap);
            if(gap>50)slowSamples++;
        }
        previousDragSample=now;dragSamples++;
    }
    internal void Finish(int width,int height,int renderTier)
    {
        using var process=Process.GetCurrentProcess();
        var report=new {
            Engine=engine,CreatedUtc=DateTime.UtcNow,DurationMs=clock.Elapsed.TotalMilliseconds,
            ProcessCpuMs=(process.TotalProcessorTime-initialCpu).TotalMilliseconds,
            WorkingSetBytes=process.WorkingSet64,WorkingSetDeltaBytes=process.WorkingSet64-initialMemory,
            ManagedAllocatedBytes=GC.GetTotalAllocatedBytes()-initialAllocated,
            Width=width,Height=height,RenderTier=renderTier,DragSamples=dragSamples,
            MaxMovingSampleGapMs=maxDragGap,MovingSampleGapsOver50Ms=slowSamples,
            Note="Process-wide resources; sample gaps include time between mouse events, not measured device latency."
        };
        // One small summary per session. No images, titles, coordinates or credentials.
        _=Task.Run(()=> {
            try {
                string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IMGViewer","Diagnostics");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,"capture-performance.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {Debug.WriteLine("IMGViewer: no se pudo escribir el diagnóstico de captura.");}
        });
    }
}
