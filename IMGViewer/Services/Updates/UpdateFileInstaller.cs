using System.IO;
using System.Diagnostics;
namespace IMGViewer.Services.Updates;

public static class UpdateFileInstaller
{
    // Stage on the destination volume before the atomic replacement.
    public static async Task ReplaceAndRestart(string candidate, string target, string newHash, string oldHash, Action<string> restart)
    {
        string stage = target + ".update-" + Guid.NewGuid().ToString("N") + ".tmp";
        string backup = target + ".previous";
        bool replaced = false;
        try
        {
            if (!string.Equals(await UpdateManifest.HashFile(candidate), newHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El archivo descargado cambió antes de instalarlo.");
            if (!string.Equals(await UpdateManifest.HashFile(target), oldHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La aplicación fue modificada por otro proceso. No se reemplazó.");
            File.Copy(candidate, stage, false);
            if (!string.Equals(await UpdateManifest.HashFile(stage), newHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("No se pudo verificar la copia de instalación.");
            for (int attempt = 0; ; attempt++)
            {
                try { File.Replace(stage, target, backup, true); break; }
                catch (IOException) when (attempt < 9) { await Task.Delay(500); }
            }
            replaced = true;
            if (!string.Equals(await UpdateManifest.HashFile(backup), oldHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La versión anterior cambió durante la instalación.");
            restart(target);
        }
        catch
        {
            if (replaced && File.Exists(backup))
            {
                File.Copy(backup, stage, true);
                File.Replace(stage, target, null, true);
            }
            throw;
        }
        finally { if (File.Exists(stage)) File.Delete(stage); }
    }

    public static void Restart(string target)
    {
        using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target)! });
        if (process is null) throw new InvalidOperationException("No se pudo reiniciar la aplicación.");
        if (process.WaitForExit(1500) && process.ExitCode != 0)
            throw new InvalidOperationException("La nueva versión no pudo iniciarse.");
    }
}
