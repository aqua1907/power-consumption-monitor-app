using PowerMonitor.Core.Presentation;

namespace PowerMonitor.Tests;

public sealed class HistoryPersistenceTests
{
    [Fact]
    public void PowerHistoryStore_round_trips_samples()
    {
        var path = TempPath("history.json");
        var now = DateTimeOffset.UtcNow;
        var samples = new[]
        {
            new PowerSample(now.AddMinutes(-10), 120, 340),
            new PowerSample(now, -450, 320),
        };

        PowerHistoryStore.Save(samples, path);
        var loaded = PowerHistoryStore.Load(path);

        Assert.Equal(samples, loaded);
    }

    [Fact]
    public void PowerHistoryStore_returns_empty_when_file_is_missing()
    {
        var path = TempPath("missing-history.json");
        Assert.Empty(PowerHistoryStore.Load(path));
    }

    [Fact]
    public void PowerHistoryStore_ignores_a_corrupt_file()
    {
        var path = TempPath("corrupt-history.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json ] ");

        Assert.Empty(PowerHistoryStore.Load(path));
    }

    [Fact]
    public void PowerHistoryCsv_writes_a_header_and_one_row_per_sample()
    {
        var path = TempPath("export.csv");
        var now = new DateTimeOffset(2026, 11, 2, 21, 30, 0, TimeSpan.Zero);
        var samples = new[]
        {
            new PowerSample(now.AddMinutes(-1), 100, 250),
            new PowerSample(now, -200, 260),
        };

        var written = PowerHistoryCsv.Write(samples, path);
        var lines = File.ReadAllLines(written);

        Assert.Equal(path, written);
        Assert.Equal("Time,BatteryWatts,LoadWatts", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.Contains("100,250", lines[1]);
        Assert.Contains("-200,260", lines[2]);
    }

    [Fact]
    public void OutageLogStore_round_trips_events()
    {
        var path = TempPath("outages.json");
        var now = DateTimeOffset.UtcNow;
        var events = new[]
        {
            new OutageEvent(now.AddHours(-2), now.AddHours(-1)),
            new OutageEvent(now.AddMinutes(-5), null),
        };

        OutageLogStore.Save(events, path);
        var loaded = OutageLogStore.Load(path);

        Assert.Equal(events, loaded);
    }

    private static string TempPath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "power-monitor-tests", Guid.NewGuid().ToString("N"), fileName);
}
