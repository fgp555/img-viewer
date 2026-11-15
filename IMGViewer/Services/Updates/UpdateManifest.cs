using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace IMGViewer.Services.Updates;

public sealed record UpdateManifest(string Version, string DownloadUrl, string Sha256, string ReleaseNotes)
{
    public const string FeedUrl = "https://cdn.frankgp.com/img-viewer/downloads/latest.json";
    public const string ExecutableUrl = "https://cdn.frankgp.com/img-viewer/downloads/IMGViewer.exe";
    public static Version CurrentVersion => Normalize(typeof(UpdateManifest).Assembly.GetName().Version!);
    public static string CurrentVersionText => CurrentVersion.ToString(3);
    public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    public Version ParsedVersion => Normalize(System.Version.Parse(Version));
    public bool IsNewerThan(Version installed) => ParsedVersion > Normalize(installed);

    public static UpdateManifest Parse(string json)
    {
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("El catÃ¡logo de actualizaciones estÃ¡ vacÃ­o.");
        if (manifest.Version is null || !Regex.IsMatch(manifest.Version, @"^\d+\.\d+\.\d+$") || !System.Version.TryParse(manifest.Version, out _))
            throw new InvalidOperationException("La versiÃ³n publicada no es vÃ¡lida.");
        if (manifest.DownloadUrl != ExecutableUrl || manifest.Sha256 is null || !Regex.IsMatch(manifest.Sha256, "^[a-fA-F0-9]{64}$"))
            throw new InvalidOperationException("El catÃ¡logo contiene una descarga no vÃ¡lida.");
        if (manifest.ReleaseNotes is null || manifest.ReleaseNotes.Length > 4000)
            throw new InvalidOperationException("Las notas de la versiÃ³n no son vÃ¡lidas.");
        return manifest;
    }

    public static async Task<string> HashFile(string path, CancellationToken token = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
    }

    public async Task VerifyExecutable(string path, CancellationToken token)
    {
        if (!string.Equals(await HashFile(path, token), Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La descarga no coincide con la versiÃ³n publicada. IntÃ©ntalo de nuevo mÃ¡s tarde.");
        var info = FileVersionInfo.GetVersionInfo(path);
        var version = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        if (Normalize(version) != ParsedVersion)
            throw new InvalidOperationException("La versiÃ³n del ejecutable no coincide con el catÃ¡logo.");
    }
}
