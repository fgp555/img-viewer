using System.Diagnostics;
using System.IO;
using System.Text.Json;
using IMGViewer.Services.Updates;

static class HelperScenario
{
    private static async Task Cleanup(string directory)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); return; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { await Task.Delay(250); }
        }
        Console.WriteLine("Temporary test directory retained: " + directory);
    }
    public static async Task Run(string helperSource, string oldHost, string newHost)
    {
        string root = Path.Combine(Path.GetTempPath(), "IMGViewer-helper-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string target = Path.Combine(root, "IMGViewer.exe");
        File.Copy(oldHost, target);
        string job = UpdateInstaller.NewJob();
        Process? parent = null, helper = null;
        try
        {
            string started = Path.Combine(root, "started.txt"), stop = Path.Combine(root, "stop.txt");
            var parentStart = new ProcessStartInfo(target) { UseShellExecute = false, CreateNoWindow = true };
            parentStart.ArgumentList.Add("--parent"); parentStart.ArgumentList.Add(started); parentStart.ArgumentList.Add(stop);
            parent = Process.Start(parentStart)!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            while (!File.Exists(started)) { if (parent.HasExited) throw new Exception("Fixture exited early"); await Task.Delay(100, timeout.Token); }
            string newHash = await UpdateManifest.HashFile(newHost), oldHash = await UpdateManifest.HashFile(target);
            var manifest = new UpdateManifest("2026.11.2", UpdateManifest.ExecutableUrl, newHash, "Test only");
            File.Copy(newHost, Path.Combine(job, "download.exe"));
            string updater = Path.Combine(job, "updater.exe"); File.Copy(helperSource, updater);
            var request = new UpdateRequest(parent.Id, parent.StartTime.ToUniversalTime().Ticks, target, oldHash, manifest, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
            string requestFile = Path.Combine(job, "request.json");
            await File.WriteAllTextAsync(requestFile, JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var helperStart = new ProcessStartInfo(updater) { UseShellExecute = false, WorkingDirectory = job, CreateNoWindow = true };
            helperStart.ArgumentList.Add("--apply-update"); helperStart.ArgumentList.Add(requestFile);
            helper = Process.Start(helperStart)!;
            string ready = Path.Combine(job, "ready.txt");
            while (!File.Exists(ready)) { if (helper.HasExited) throw new Exception("Helper exited before handshake"); await Task.Delay(100, timeout.Token); }
            if (await UpdateManifest.HashFile(target) != oldHash) throw new Exception("Helper replaced a running app");
            await File.WriteAllTextAsync(stop, "exit");
            await helper.WaitForExitAsync(timeout.Token);
            // Process.Start can succeed before a busy machine schedules the new process.
            string restarted = Path.Combine(root, "restarted.txt");
            for (int attempt=0; helper.ExitCode==0 && !File.Exists(restarted) && attempt<50; attempt++)
                await Task.Delay(100, timeout.Token);
            if (helper.ExitCode != 0 || await UpdateManifest.HashFile(target) != newHash || await UpdateManifest.HashFile(target + ".previous") != oldHash || !File.Exists(Path.Combine(root, "restarted.txt")))
                throw new Exception($"Replacement, backup or restart failed: helper={helper.ExitCode}; target={await UpdateManifest.HashFile(target)} expected={newHash}; backup={File.Exists(target + ".previous")}; restarted={File.Exists(Path.Combine(root, "restarted.txt"))}; result={(File.Exists(Path.Combine(job,"result.txt")) ? await File.ReadAllTextAsync(Path.Combine(job,"result.txt")) : "missing")}");
            Console.WriteLine("PASS: published helper handshake, waits for parent, replaces a real EXE, preserves backup and starts new version.");
        }
        finally
        {
            foreach (var process in new[] { helper, parent })
                if (process is not null) { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } process.Dispose(); }
            await Cleanup(job);
            await Cleanup(root);
        }
    }
}

