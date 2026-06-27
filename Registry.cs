using Microsoft.Win32;

namespace GpuPreference;

public enum GpuPref { Default = 0, PowerSaving = 1, HighPerformance = 2 }

public record GpuEntry(string Exe, string Name, GpuPref Pref);

public static class GpuRegistry
{
    const string RegPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    public static List<GpuEntry> ListEntries()
    {
        var result = new List<GpuEntry>();
        using var key = Registry.CurrentUser.OpenSubKey(RegPath);
        if (key is null) return result;

        foreach (var name in key.GetValueNames())
        {
            var raw = key.GetValue(name) as string ?? "";
            result.Add(new GpuEntry(name, Path.GetFileName(name), ParsePref(raw)));
        }
        return result;
    }

    public static void SetEntry(string exe, GpuPref pref)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegPath);
        key.SetValue(exe, $"GpuPreference={(int)pref};", RegistryValueKind.String);
    }

    public static void DeleteEntry(string exe)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
        key?.DeleteValue(exe, throwOnMissingValue: false);
    }

    static GpuPref ParsePref(string raw)
    {
        var idx = raw.IndexOf("GpuPreference=", StringComparison.Ordinal);
        if (idx < 0) return GpuPref.Default;
        var val = raw[(idx + 14)..].TrimEnd(';');
        return int.TryParse(val, out var n) && Enum.IsDefined(typeof(GpuPref), n)
            ? (GpuPref)n : GpuPref.Default;
    }
}
