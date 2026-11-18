using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace IMGViewer;

internal static class CapturePreferences
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IMGViewer", "capture-folder-v2.txt");

    internal static bool ShowThumbnail
    {
        get { using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\IMGViewer"); return key?.GetValue("CaptureThumbnail") is not 0; }
        set { using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\IMGViewer"); key.SetValue("CaptureThumbnail", value ? 1 : 0); }
    }
    internal static bool ThumbnailOnPrimaryMonitor
    {
        get { using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\IMGViewer"); return key?.GetValue("CaptureThumbnailOnPrimaryMonitor") is not 0; }
        set { using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\IMGViewer"); key.SetValue("CaptureThumbnailOnPrimaryMonitor", value ? 1 : 0); }
    }
    internal static int ThumbnailSeconds
    {
        get { using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\IMGViewer"); return key?.GetValue("CaptureThumbnailSeconds") is int seconds ? Math.Clamp(seconds, 3, 60) : 8; }
        set { using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\IMGViewer"); key.SetValue("CaptureThumbnailSeconds", Math.Clamp(value, 3, 60)); }
    }
    internal static bool AutoSave
    {
        get { using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\IMGViewer"); return key?.GetValue("AutoSaveCaptures") is 1; }
        set { using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\IMGViewer"); key.SetValue("AutoSaveCaptures", value ? 1 : 0); }
    }
    internal static string FormatExtension
    {
        get
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\IMGViewer");
            string? ext = key?.GetValue("CaptureFormat") as string;
            return ext is ".webp" or ".png" or ".jpg" or ".bmp" ? ext : ".jpg";
        }
        set { using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\IMGViewer"); key.SetValue("CaptureFormat", value); }
    }

    internal static string CaptureFolder
    {
        get
        {
            try
            {
                string folder = File.ReadAllText(SettingsPath);
                if (Path.IsPathFullyQualified(folder))
                {
                    return folder;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }
    }

    internal static void SetFolder(string? folder)
    {
        if (folder is null)
        {
            if (Directory.Exists(Path.GetDirectoryName(SettingsPath)))
            {
                File.Delete(SettingsPath);
            }

            return;
        }

        string directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, Path.GetRandomFileName());
        try
        {
            File.WriteAllText(temporary, folder);
            File.Move(temporary, SettingsPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    internal static string FileName(DateTime timestamp) =>
        "screen-" + timestamp.ToString("yyMMdd-HHmmss", CultureInfo.InvariantCulture) + FormatExtension;

    private static string DownloadsFolder()
    {
        Guid downloads = new("374DE290-123F-4565-9164-39C4925E467B");
        const uint dontVerify = 0x00004000;
        int result = SHGetKnownFolderPath(ref downloads, dontVerify, 0, out nint path);
        try
        {
            return (result == 0 ? Marshal.PtrToStringUni(path) : null)
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
        finally
        {
            Marshal.FreeCoTaskMem(path);
        }
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(ref Guid folder, uint flags, nint token, out nint path);
}
