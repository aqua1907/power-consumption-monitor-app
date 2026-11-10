using PowerMonitor.Core.Settings;

namespace PowerMonitor.Tests;

public sealed class WindowLayoutStoreTests
{
    [Fact]
    public void Sizes_round_trip_per_window()
    {
        var path = TempPath();

        WindowLayoutStore.Set("status", new WindowSize(480, 560), path);
        WindowLayoutStore.Set("history", new WindowSize(700, 400), path);

        Assert.Equal(new WindowSize(480, 560), WindowLayoutStore.Get("status", path));
        Assert.Equal(new WindowSize(700, 400), WindowLayoutStore.Get("history", path));
        Assert.Null(WindowLayoutStore.Get("settings", path));
    }

    [Fact]
    public void Missing_file_returns_nothing() =>
        Assert.Null(WindowLayoutStore.Get("status", TempPath()));

    [Fact]
    public void Corrupt_file_is_ignored_and_replaced()
    {
        var path = TempPath();
        File.WriteAllText(path, "{ not json");

        Assert.Null(WindowLayoutStore.Get("status", path));
        WindowLayoutStore.Set("status", new WindowSize(400, 500), path);
        Assert.Equal(new WindowSize(400, 500), WindowLayoutStore.Get("status", path));
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(-5, 300)]
    [InlineData(400, double.NaN)]
    [InlineData(400, 1_000_000)]
    public void Invalid_sizes_are_not_saved(double width, double height)
    {
        var path = TempPath();

        WindowLayoutStore.Set("status", new WindowSize(width, height), path);

        Assert.Null(WindowLayoutStore.Get("status", path));
    }

    private static string TempPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "power-monitor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "window-layout.json");
    }
}
