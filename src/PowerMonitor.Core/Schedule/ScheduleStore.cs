using System.Text.Json;

namespace PowerMonitor.Core.Schedule;

/// <summary>Persists the <see cref="ScheduleArchive"/> at <c>%AppData%\PowerMonitor\schedule.json</c>.</summary>
public static class ScheduleStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PowerMonitor",
        "schedule.json");

    public static ScheduleArchive Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
                return new ScheduleArchive();

            var saved = JsonSerializer.Deserialize<SavedArchive>(File.ReadAllText(path));
            return saved is null
                ? new ScheduleArchive()
                : new ScheduleArchive(saved.OperatorId ?? "", saved.Group ?? "", saved.Days, saved.Watched);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new ScheduleArchive();
        }
    }

    public static void Save(ScheduleArchive archive, string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var saved = new SavedArchive
            {
                OperatorId = archive.OperatorId,
                Group = archive.Group,
                Days = archive.Days.Values.ToArray(),
                Watched = archive.Watched.ToArray()
            };
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(saved));
            File.Move(temporary, path, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class SavedArchive
    {
        public string? OperatorId { get; set; }
        public string? Group { get; set; }
        public DaySchedule[]? Days { get; set; }
        public WatchSpan[]? Watched { get; set; }
    }
}
