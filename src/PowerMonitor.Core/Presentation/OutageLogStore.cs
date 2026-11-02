using System.Text.Json;

namespace PowerMonitor.Core.Presentation;

/// <summary>
/// Persists the outage (grid-loss) event log so it survives app restarts.
/// </summary>
public static class OutageLogStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PowerMonitor",
        "outages.json");

    public static IReadOnlyList<OutageEvent> Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
                return [];

            var json = File.ReadAllText(path);
            var events = JsonSerializer.Deserialize<OutageEvent[]>(json);
            return events ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static void Save(IReadOnlyList<OutageEvent> events, string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(events);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
