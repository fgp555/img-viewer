using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;

namespace IMGViewer;

internal static class DefaultAppRegistration
{
    private const string CapabilitiesPath = @"Software\IMGViewer\Capabilities";
    private const string ProgId = "IMGViewer.Image";

    internal static void RegisterCurrentExecutable()
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("No se encontró el ejecutable de IMG Viewer.");
        if (!File.Exists(executable) || !Path.GetFileNameWithoutExtension(executable).StartsWith("IMGViewer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ejecuta IMGViewer.exe desde la ubicación donde lo conservarás para registrar el visor.");
        string command = "\"" + executable + "\" \"%1\"";
        using (var program = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
        {
            program.SetValue("", "Imagen de IMG Viewer");
            program.SetValue("FriendlyTypeName", "Imagen de IMG Viewer");
            using var icon = program.CreateSubKey("DefaultIcon"); icon.SetValue("", "\"" + executable + "\",0");
            using var open = program.CreateSubKey(@"shell\open\command"); open.SetValue("", command);
        }
        using (var capabilities = Registry.CurrentUser.CreateSubKey(CapabilitiesPath))
        {
            capabilities.SetValue("ApplicationName", "IMG Viewer");
            capabilities.SetValue("ApplicationDescription", "Visor de imágenes, capturas y marcación.");
            capabilities.SetValue("ApplicationIcon", "\"" + executable + "\",0");
            using var associations = capabilities.CreateSubKey("FileAssociations");
            foreach (string extension in ViewerPreferences.AvailableExtensions) associations.SetValue(extension, ProgId);
        }
        using (var registered = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            registered.SetValue("IMGViewer", CapabilitiesPath);
        using (var application = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\" + Path.GetFileName(executable)))
        {
            application.SetValue("FriendlyAppName", "IMG Viewer");
            using var open = application.CreateSubKey(@"shell\open\command"); open.SetValue("", command);
            using var supported = application.CreateSubKey("SupportedTypes");
            foreach (string extension in ViewerPreferences.AvailableExtensions) supported.SetValue(extension, "");
        }
        foreach (string extension in ViewerPreferences.AvailableExtensions)
        {
            using var openWith = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + extension + @"\OpenWithProgids");
            openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }
        // Refresh Shell registration; never modify UserChoice or force default associations.
        SHChangeNotify(0x08000000, 0, 0, 0);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, nint item1, nint item2);
}
