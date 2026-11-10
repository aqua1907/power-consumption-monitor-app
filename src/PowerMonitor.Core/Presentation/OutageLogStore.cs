using System.Text.Json;

namespace PowerMonitor.Core.Presentation;

/// <summary>
/// Persists the outage log and the last grid reading, so a restart can tell that the grid
/// changed while this PC was off.
/// </summary>
public static class OutageLogStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PowerMonitor",
        "outages.json");

    public static OutageLog Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
                return new OutageLog();

            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var events = JsonSerializer.Deserialize<OutageEvent[]>(json);
                return new OutageLog(events);
            }

            var saved = JsonSerializer.Deserialize<SavedOutages>(json);
            return new OutageLog(saved?.Events, saved?.LastSeenAt, saved?.LastGridConnected);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new OutageLog();
        }
    }

    public static void Save(OutageLog log, string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var saved = new SavedOutages
            {
                Events = log.Events.ToArray(),
                LastSeenAt = log.LastSeenAt,
                LastGridConnected = log.LastGridConnected
            };
            var json = JsonSerializer.Serialize(saved);
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

    private sealed class SavedOutages
    {
        public OutageEvent[]? Events { get; set; }
        public DateTimeOffset? LastSeenAt { get; set; }
        public bool? LastGridConnected { get; set; }
    }
}
