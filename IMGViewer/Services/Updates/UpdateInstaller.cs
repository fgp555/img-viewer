using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace IMGViewer.Services.Updates;

public sealed record UpdateRequest(int ParentId, long ParentStartedUtcTicks, string Target, string OldHash, UpdateManifest Manifest, string Nonce, DateTimeOffset CreatedAt);

public static class UpdateInstaller
{
    public static string JobsRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IMGViewer", "updates");
    public static bool IsPublishedExecutable => typeof(UpdateInstaller).Assembly.GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
        .Any(item => item.Key == "SingleFileUpdate" && string.Equals(item.Value, "true", StringComparison.OrdinalIgnoreCase)) &&
        !string.Equals(Path.GetFileName(Environment.ProcessPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase);
    public static bool UsesExternalRuntime => !typeof(UpdateInstaller).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Any(item => item.Key == "SelfContainedPackage" && string.Equals(item.Value, "true", StringComparison.OrdinalIgnoreCase));
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string NewJob()
    {
        string path = Path.Combine(JobsRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }

    public static void CleanupOldJobs()
    {
        if (!IsPublishedExecutable || !Directory.Exists(JobsRoot)) return;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "updater.exe", "request.json", "ready.txt", "result.txt", "download.exe" };
        DateTime cutoff = DateTime.UtcNow.AddDays(-7);
        try
        {
            if ((File.GetAttributes(JobsRoot) & FileAttributes.ReparsePoint) != 0) return;
            foreach (string directory in Directory.EnumerateDirectories(JobsRoot))
            {
                try
                {
                    if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 || Directory.GetLastWriteTimeUtc(directory) >= cutoff || Directory.EnumerateDirectories(directory).Any()) continue;
                    var files = Directory.GetFiles(directory);
                    if (files.Any(file => !allowed.Contains(Path.GetFileName(file)) || File.GetLastWriteTimeUtc(file) >= cutoff || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)) continue;
                    foreach (string file in files) File.Delete(file);
                    Directory.Delete(directory);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    public static async Task Prepare(string job, UpdateManifest manifest, CancellationToken token)
    {
        if (!IsPublishedExecutable || !UsesExternalRuntime)
            throw new InvalidOperationException("Para actualizar, abre el ejecutable de producciÃ³n publicado en la carpeta publish.");
        string target = Environment.ProcessPath!;
        // Check folder permissions without requesting elevation or closing the app.
        string probe = Path.Combine(Path.GetDirectoryName(target)!, ".imgviewer-write-" + Guid.NewGuid().ToString("N"));
        try { using (File.Create(probe)) { } }
        catch (UnauthorizedAccessException) { throw new InvalidOperationException("La carpeta no permite reemplazar la aplicaciÃ³n. MuÃ©vela a una carpeta de tu usuario."); }
        finally { if (File.Exists(probe)) File.Delete(probe); }
        await manifest.VerifyExecutable(Path.Combine(job, "download.exe"), token);
        using var parent = Process.GetCurrentProcess();
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var request = new UpdateRequest(parent.Id, parent.StartTime.ToUniversalTime().Ticks, target, await UpdateManifest.HashFile(target, token), manifest, nonce, DateTimeOffset.UtcNow);
        string requestFile = Path.Combine(job, "request.json");
        await File.WriteAllTextAsync(requestFile, JsonSerializer.Serialize(request, Json), token);
        string helper = Path.Combine(job, "updater.exe"); File.Copy(target, helper, true);
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, WorkingDirectory = job, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(requestFile);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo abrir el actualizador.");
        try
        {
            string ready = Path.Combine(job, "ready.txt");
            for (int attempt = 0; attempt < 150; attempt++)
            {
                token.ThrowIfCancellationRequested();
                if (File.Exists(ready) && await File.ReadAllTextAsync(ready, token) == nonce) return;
                if (process.HasExited) break;
                await Task.Delay(100, token);
            }
            throw new InvalidOperationException("El actualizador no respondiÃ³. La aplicaciÃ³n permanece abierta.");
        }
        catch
        {
            if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); }
            throw;
        }
    }
    public static async Task Apply(string requestFile)
    {
        if (!IsPublishedExecutable || !UsesExternalRuntime) throw new InvalidOperationException("Actualizador no disponible en este entorno.");
        string path = Path.GetFullPath(requestFile); string job = Path.GetDirectoryName(path)!;
        if (Path.GetFileName(path) != "request.json" || !string.Equals(Path.GetDirectoryName(job), JobsRoot, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileName(job), "N", out _) || !string.Equals(Environment.ProcessPath, Path.Combine(job, "updater.exe"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Solicitud de actualizaciÃ³n no vÃ¡lida.");
        var request = JsonSerializer.Deserialize<UpdateRequest>(await File.ReadAllTextAsync(path), Json) ?? throw new InvalidOperationException("Solicitud vacÃ­a.");
        _ = UpdateManifest.Parse(JsonSerializer.Serialize(request.Manifest, Json));
        if (DateTimeOffset.UtcNow - request.CreatedAt > TimeSpan.FromMinutes(10) || request.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(1) ||
            !Path.IsPathFullyQualified(request.Target) || !string.Equals(Path.GetExtension(request.Target), ".exe", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFullPath(request.Target).StartsWith(JobsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !request.Manifest.IsNewerThan(UpdateManifest.CurrentVersion))
            throw new InvalidOperationException("La solicitud venciÃ³ o no corresponde a una versiÃ³n nueva.");
        using var parent = Process.GetProcessById(request.ParentId);
        if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStartedUtcTicks || !string.Equals(parent.MainModule?.FileName, request.Target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("No se encontrÃ³ la aplicaciÃ³n que solicitÃ³ la actualizaciÃ³n.");
        string candidate = Path.Combine(job, "download.exe");
        await request.Manifest.VerifyExecutable(candidate, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(job, "ready.txt"), request.Nonce);
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await parent.WaitForExitAsync(wait.Token);
        await UpdateFileInstaller.ReplaceAndRestart(candidate, request.Target, request.Manifest.Sha256, request.OldHash, UpdateFileInstaller.Restart);
        try { await File.WriteAllTextAsync(Path.Combine(job, "result.txt"), "Updated " + request.Manifest.Version); File.Delete(candidate); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

