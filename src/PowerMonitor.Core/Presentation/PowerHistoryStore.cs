using System.Text.Json;

namespace PowerMonitor.Core.Presentation;

/// <summary>
/// Persists the rolling battery/load history so the chart survives app restarts.
/// </summary>
public static class PowerHistoryStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PowerMonitor",
        "history.json");

    public static IReadOnlyList<PowerSample> Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
                return [];

            var json = File.ReadAllText(path);
            var samples = JsonSerializer.Deserialize<PowerSample[]>(json);
            return samples ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static void Save(IReadOnlyList<PowerSample> samples, string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(samples);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        catch (IOException)
        {
            // Best effort: losing the on-disk backup is not worth crashing the monitor loop over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
