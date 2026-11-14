using Microsoft.Win32;

namespace IMGViewer;

internal static class ViewerPreferences
{
    private const string SettingsKey = @"Software\IMGViewer";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValue = "IMGViewer";

    internal static readonly string[] AvailableExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".svg", ".webp"];
    private static HashSet<string>? enabledExtensions;
    internal static HashSet<string> EnabledExtensions
    {
        get
        {
            if (enabledExtensions is not null) return enabledExtensions;
            using var settings = Registry.CurrentUser.OpenSubKey(SettingsKey);
            var saved = settings?.GetValue("EnabledExtensions") as string[];
            return enabledExtensions = new HashSet<string>((saved ?? AvailableExtensions).Where(ext => AvailableExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);
        }
    }
    internal static void SetEnabledExtensions(IEnumerable<string> extensions)
    {
        string[] selected = extensions.Where(ext => AvailableExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        using var settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
        settings.SetValue("EnabledExtensions", selected, RegistryValueKind.MultiString);
        enabledExtensions = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
    }

    internal const int DefaultGridColumns = 3;
    internal const int MaximumGridColumns = 8;

    internal static int BackgroundOpacityPercent
    {
        get
        {
            using RegistryKey? settings = Registry.CurrentUser.OpenSubKey(SettingsKey);
            return settings?.GetValue(nameof(BackgroundOpacityPercent)) is int percent && percent is >= 1 and <= 100
                ? percent
                : 25;
        }
        set
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            settings.SetValue(nameof(BackgroundOpacityPercent), Math.Clamp(value, 1, 100), RegistryValueKind.DWord);
        }
    }

    internal static int GridColumns
    {
        get
        {
            using RegistryKey? settings = Registry.CurrentUser.OpenSubKey(SettingsKey);
            return settings?.GetValue(nameof(GridColumns)) is int count && count is >= 1 and <= MaximumGridColumns
                ? count
                : DefaultGridColumns;
        }
        set
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            settings.SetValue(nameof(GridColumns), value, RegistryValueKind.DWord);
        }
    }

    internal const int DefaultCursorIdleSeconds = 3;
    internal const int MaximumCursorIdleSeconds = 30;

    internal static bool HideIdleCursor
    {
        get
        {
            using RegistryKey? settings = Registry.CurrentUser.OpenSubKey(SettingsKey);
            return settings?.GetValue(nameof(HideIdleCursor)) is 1;
        }
        set
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            settings.SetValue(nameof(HideIdleCursor), value ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    internal static bool CursorAcrossWindows
    {
        get
        {
            using RegistryKey? settings = Registry.CurrentUser.OpenSubKey(SettingsKey);
            return settings?.GetValue(nameof(CursorAcrossWindows)) is 1;
        }
        set
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            settings.SetValue(nameof(CursorAcrossWindows), value ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    internal static int CursorIdleSeconds
    {
        get
        {
            using RegistryKey? settings = Registry.CurrentUser.OpenSubKey(SettingsKey);
            return settings?.GetValue(nameof(CursorIdleSeconds)) is int seconds && seconds is >= 1 and <= MaximumCursorIdleSeconds
                ? seconds
                : DefaultCursorIdleSeconds;
        }
        set
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            settings.SetValue(nameof(CursorIdleSeconds), value, RegistryValueKind.DWord);
        }
    }

    internal static int WebPQuality
    {
        get { using var settings = Registry.CurrentUser.OpenSubKey(SettingsKey); return settings?.GetValue(nameof(WebPQuality)) is int quality ? Math.Clamp(quality, 1, 100) : 80; }
        set { using var settings = Registry.CurrentUser.CreateSubKey(SettingsKey); settings.SetValue(nameof(WebPQuality), Math.Clamp(value, 1, 100), RegistryValueKind.DWord); }
    }
    internal static bool WebPLossless
    {
        get { using var settings = Registry.CurrentUser.OpenSubKey(SettingsKey); return settings?.GetValue(nameof(WebPLossless)) is 1; }
        set { using var settings = Registry.CurrentUser.CreateSubKey(SettingsKey); settings.SetValue(nameof(WebPLossless), value ? 1 : 0, RegistryValueKind.DWord); }
    }

    internal static bool MinimizeAfterCopy
    {
        get { using var settings = Registry.CurrentUser.OpenSubKey(SettingsKey); return settings?.GetValue(nameof(MinimizeAfterCopy)) is 1; }
        set { using var settings = Registry.CurrentUser.CreateSubKey(SettingsKey); settings.SetValue(nameof(MinimizeAfterCopy), value ? 1 : 0, RegistryValueKind.DWord); }
    }

    internal static bool CopyCapturesToClipboard
    {
        get
        {
            using RegistryKey? settings = Registry.CurrentUser.OpenSubKey(SettingsKey);
            return settings?.GetValue(nameof(CopyCapturesToClipboard)) is 1;
        }
        set
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            settings.SetValue(nameof(CopyCapturesToClipboard), value ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    internal static bool CloseToTray
    {
        get
        {
            using RegistryKey? settings = Registry.CurrentUser.OpenSubKey(SettingsKey);
            return settings?.GetValue(nameof(CloseToTray)) is not int value || value != 0;
        }
        set
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            settings.SetValue(nameof(CloseToTray), value ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    internal static bool StartWithWindows
    {
        get
        {
            using RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKey);
            return run?.GetValue(StartupValue) is string;
        }
        set
        {
            using RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
            {
                string executable = Environment.ProcessPath ?? throw new InvalidOperationException("No se encontró el ejecutable.");
                run.SetValue(StartupValue, $"\"{executable}\" {App.BackgroundArgument}", RegistryValueKind.String);
            }
            else
            {
                run.DeleteValue(StartupValue, throwOnMissingValue: false);
            }
        }
    }
}
