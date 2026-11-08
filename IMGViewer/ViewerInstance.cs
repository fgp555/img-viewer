using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace IMGViewer;

internal sealed class ViewerInstance : IDisposable
{
    private readonly string name;
    private readonly Mutex mutex;
    private readonly CancellationTokenSource stopping = new();

    internal bool IsPrimary { get; }

    internal ViewerInstance()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        using Process process = Process.GetCurrentProcess();
        name = $"IMGViewer-{identity.User?.Value}-{process.SessionId}";
        mutex = new Mutex(false, @"Local\" + name, out bool primary);
        IsPrimary = primary;
    }

    internal bool Forward(string path)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(2500);
            if (GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint process))
            {
                AllowSetForegroundWindow(process);
            }

            using var writer = new StreamWriter(pipe);
            writer.WriteLine(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal void Listen(Action<string> activate)
    {
        CancellationToken token = stopping.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(name, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(pipe);
                    using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    requestTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                    string? path = await reader.ReadLineAsync(requestTimeout.Token);
                    if (path is not null && !token.IsCancellationRequested)
                    {
                        activate(path);
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException)
                {
                    try { await Task.Delay(100, token); }
                    catch (OperationCanceledException) { }
                }
            }
        });
    }

    public void Dispose()
    {
        stopping.Cancel();
        stopping.Dispose();
        mutex.Dispose();
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint process);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint process);
}
