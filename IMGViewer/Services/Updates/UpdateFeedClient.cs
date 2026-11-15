using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
namespace IMGViewer.Services.Updates;

public sealed class UpdateFeedClient : IDisposable
{
    public const long MaximumDownloadBytes = 512 * 1024 * 1024;
    private readonly HttpClient client;
    public UpdateFeedClient(HttpMessageHandler? handler = null)
    {
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IMGViewer/" + UpdateManifest.CurrentVersionText);
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
    }
    public async Task<UpdateManifest> Check(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await client.GetAsync(UpdateManifest.FeedUrl + "?check=" + Guid.NewGuid().ToString("N"), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 16 * 1024) throw new InvalidOperationException("El catálogo es demasiado grande.");
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new MemoryStream();
        await CopyLimited(stream, output, 16 * 1024, null, null, timeout.Token);
        return UpdateManifest.Parse(Encoding.UTF8.GetString(output.ToArray()));
    }
    public async Task Download(UpdateManifest manifest, string path, IProgress<double>? progress, CancellationToken token)
    {
        if (manifest.DownloadUrl != UpdateManifest.ExecutableUrl) throw new InvalidOperationException("Origen de actualización no permitido.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(5));
        using var response = await client.GetAsync(manifest.DownloadUrl + "?v=" + manifest.Sha256, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        long? length = response.Content.Headers.ContentLength;
        if (length > MaximumDownloadBytes) throw new InvalidOperationException("La descarga supera el tamaño permitido.");
        bool created = false;
        try
        {
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            { created = true; await CopyLimited(input, output, MaximumDownloadBytes, length, progress, timeout.Token); }
            await manifest.VerifyExecutable(path, timeout.Token);
            progress?.Report(100);
        }
        catch { if (created) File.Delete(path); throw; }
    }
    private static async Task CopyLimited(Stream source, Stream destination, long limit, long? length, IProgress<double>? progress, CancellationToken token)
    {
        var buffer = new byte[81920]; long total = 0; int count;
        while ((count = await source.ReadAsync(buffer, token)) != 0)
        {
            total += count; if (total > limit) throw new InvalidOperationException("La respuesta supera el tamaño permitido.");
            await destination.WriteAsync(buffer.AsMemory(0, count), token);
            if (length > 0) progress?.Report(Math.Min(99, total * 100.0 / length.Value));
        }
    }
    public void Dispose() => client.Dispose();
}
