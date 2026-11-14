using Microsoft.Win32;
using System.Windows.Input;
namespace IMGViewer;
internal readonly record struct CaptureShortcut(uint Modifiers, uint VirtualKey)
{
    internal static CaptureShortcut Default => new(2, 0x2C);
    internal string Label => ((Modifiers & 2) != 0 ? "Ctrl + " : "") + ((Modifiers & 1) != 0 ? "Alt + " : "") + ((Modifiers & 4) != 0 ? "Shift + " : "") + (VirtualKey == 0x2C ? "Impr Pant" : new KeyConverter().ConvertToString(KeyInterop.KeyFromVirtualKey((int)VirtualKey)));
    internal static CaptureShortcut Load()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\IMGViewer");
        if (key?.GetValue("CaptureShortcut") is string text)
        {
            var parts = text.Split(':');
            if (parts.Length == 2 && uint.TryParse(parts[0], out uint mods) && uint.TryParse(parts[1], out uint vk) && mods <= 7 && vk is > 0 and < 255 && (mods != 0 || vk == 0x2C)) return new(mods, vk);
        }
        return Default;
    }
    internal void Save()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\IMGViewer");
        key.SetValue("CaptureShortcut", $"{Modifiers}:{VirtualKey}");
    }
}
