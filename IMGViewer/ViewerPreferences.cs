using Microsoft.Win32;

namespace IMGViewer;

internal static class ViewerPreferences
{
    private const string SettingsKey = @"Software\IMGViewer";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValue = "IMGViewer";

    internal const int DefaultGridColumns = 3;
    internal const int MaximumGridColumns = 8;

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
