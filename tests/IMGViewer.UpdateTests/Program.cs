using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using IMGViewer.Services.Updates;


if (args.Length == 4 && args[0] == "--helper-scenario") { await HelperScenario.Run(args[1],args[2],args[3]); return; }

string temp = Path.Combine(Path.GetTempPath(), "IMGViewer-update-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    string old = Path.Combine(temp, "current.exe"), next = Path.Combine(temp, "candidate.exe");
    await File.WriteAllTextAsync(old, "previous version"); await File.WriteAllTextAsync(next, "next version");
    string oldHash = await UpdateManifest.HashFile(old), nextHash = await UpdateManifest.HashFile(next);
    string Json(string version = "0.1.2", string? url = null, string? hash = null) => JsonSerializer.Serialize(new { version, downloadUrl = url ?? UpdateManifest.ExecutableUrl, sha256 = hash ?? nextHash, releaseNotes = "Test release" });
    var manifest = UpdateManifest.Parse(Json());
    Check(manifest.IsNewerThan(new Version(0, 1, 1)), "Newer version");
    Check(!manifest.IsNewerThan(new Version(0, 1, 2, 0)), "Equal normalized version");
    Check(!manifest.IsNewerThan(new Version(0, 2, 0)), "Never downgrade");
    Reject(() => UpdateManifest.Parse(Json("invalid")), "Invalid version");
    Reject(() => UpdateManifest.Parse(Json(url: "http://cdn.frankgp.com/call/CallMonitor.exe")), "Reject HTTP");
    Reject(() => UpdateManifest.Parse(Json(url: "https://example.test/CallMonitor.exe")), "Reject foreign URL");
    Reject(() => UpdateManifest.Parse(Json(hash: "bad")), "Reject hash");

    bool restarted = false;
    await UpdateFileInstaller.ReplaceAndRestart(next, old, nextHash, oldHash, path => { Check(File.ReadAllText(path) == "next version", "Replacement visible on restart"); restarted = true; });
    Check(restarted && File.ReadAllText(old + ".previous") == "previous version", "Backup preserved");
    await File.WriteAllTextAsync(next, "third version"); string thirdHash = await UpdateManifest.HashFile(next);
    bool failed = false;
    try { await UpdateFileInstaller.ReplaceAndRestart(next, old, thirdHash, nextHash, _ => throw new IOException("Simulated restart failure")); } catch (IOException) { failed = true; }
    Check(failed && File.ReadAllText(old) == "next version", "Rollback after restart failure");
    failed = false;
    try { await UpdateFileInstaller.ReplaceAndRestart(next, old, new string('A', 64), nextHash, _ => throw new Exception("Must not restart")); } catch (InvalidOperationException) { failed = true; }
    Check(failed && File.ReadAllText(old) == "next version", "Tampered download never installed");
    failed = false;
    try { await UpdateFileInstaller.ReplaceAndRestart(next, old, thirdHash, oldHash, _ => throw new Exception("Must not restart")); } catch (InvalidOperationException) { failed = true; }
    Check(failed && File.ReadAllText(old) == "next version", "Concurrent target change rejected");

    using var feed = new UpdateFeedClient(new Handler(request => {
        Check(request.RequestUri!.Host == "cdn.frankgp.com", "Pinned host");
        Check(request.Headers.CacheControl!.NoCache && request.RequestUri.Query.Length > 0, "Avoid stale catalog");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json()) };
    }));
    Check((await feed.Check(CancellationToken.None)).Version == "0.1.2", "Read catalog");
    using var badFeed = new UpdateFeedClient(new Handler(_ => new(HttpStatusCode.Found) { Headers = { Location = new Uri("https://example.test") } }));
    try { await badFeed.Check(CancellationToken.None); throw new Exception("Redirect accepted"); } catch (HttpRequestException) { }
    using var oversized = new UpdateFeedClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(new string('a', 20000)) }));
    try { await oversized.Check(CancellationToken.None); throw new Exception("Oversized catalog accepted"); } catch (InvalidOperationException) { }
    string download = Path.Combine(temp, "download.exe");
    using var corrupted = new UpdateFeedClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("corrupt")) }));
    try { await corrupted.Download(manifest, download, null, CancellationToken.None); throw new Exception("Bad download accepted"); } catch (InvalidOperationException) { }
    Check(!File.Exists(download), "Failed download removed");
    await File.WriteAllTextAsync(download, "existing file");
    try { await corrupted.Download(manifest, download, null, CancellationToken.None); throw new Exception("Existing file overwritten"); } catch (IOException) { }
    Check(File.ReadAllText(download) == "existing file", "Existing file preserved");
    if (!UpdateInstaller.IsPublishedExecutable || !UpdateInstaller.UsesExternalRuntime)
    {
        using var disabled = new UpdateCoordinator(new UpdateFeedClient(new Handler(_ => throw new Exception("Non-production must not request updates"))));
        await disabled.Check(); Check(!disabled.Enabled && disabled.State == UpdateState.Idle, "Updates disabled outside production");
    }
    Console.WriteLine("PASS: manifest validation, version comparisons, pinned origin, cache, download verification, atomic replacement, backup, rollback and environment policy.");
}
finally
{
    foreach (string file in Directory.GetFiles(temp)) File.Delete(file);
    Directory.Delete(temp);
}
static void Check(bool value, string label) { if (!value) throw new Exception(label); }
static void Reject(Action action, string label) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception(label); }
sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(handle(request));
}
