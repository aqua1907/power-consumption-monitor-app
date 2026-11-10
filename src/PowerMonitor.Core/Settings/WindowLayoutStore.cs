using System.Text.Json;

namespace PowerMonitor.Core.Settings;

/// <summary>A window size in device-independent pixels, so it stays correct at any monitor DPI.</summary>
public sealed record WindowSize(double Width, double Height)
{
    public bool IsValid =>
        double.IsFinite(Width) && double.IsFinite(Height) &&
        Width is > 0 and <= 20_000 && Height is > 0 and <= 20_000;
}

/// <summary>Remembers the sizes the user gave each window. Kept apart from <see cref="AppSettings"/>.</summary>
public static class WindowLayoutStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PowerMonitor",
        "window-layout.json");

    public static WindowSize? Get(string key, string? path = null) =>
        Load(path).GetValueOrDefault(key);

    public static void Set(string key, WindowSize size, string? path = null)
    {
        if (!size.IsValid)
            return;

        path ??= DefaultPath;
        var layout = Load(path);
        if (layout.TryGetValue(key, out var existing) && existing == size)
            return;

        layout[key] = size;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(layout, SettingsStore.JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    private static Dictionary<string, WindowSize> Load(string? path)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
                return [];

            var loaded = JsonSerializer.Deserialize<Dictionary<string, WindowSize?>>(
                File.ReadAllText(path), SettingsStore.JsonOptions);
            return loaded is null
                ? []
                : loaded.Where(pair => pair.Value is { IsValid: true })
                    .ToDictionary(pair => pair.Key, pair => pair.Value!);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
