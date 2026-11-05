using PowerMonitor.Core.Games;
using PowerMonitor.Core.Settings;
using PowerMonitor.Core.Wow;

namespace PowerMonitor.Tests;

public class WowConfigPatcherTests
{
    [Fact]
    public void ApplyCvars_updates_existing_keys_and_appends_missing_ones()
    {
        var directory = Path.Combine(Path.GetTempPath(), "power-monitor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Config.wtf");
        File.WriteAllLines(path,
        [
            "SET graphicsQuality \"6\"",
            "SET portal \"EU\"",
            "SET vsync \"0\""
        ]);

        WowConfigPatcher.ApplyCvars(path, new Dictionary<string, string>
        {
            ["graphicsQuality"] = "1",
            ["maxFPS"] = "40",
            ["vsync"] = "1"
        });

        var text = File.ReadAllText(path);
        Assert.Contains("SET graphicsQuality \"1\"", text);
        Assert.Contains("SET portal \"EU\"", text);
        Assert.Contains("SET vsync \"1\"", text);
        Assert.Contains("SET maxFPS \"40\"", text);
        Assert.DoesNotContain("SET graphicsQuality \"6\"", text);
    }

    [Fact]
    public void Snapshot_and_restore_round_trip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "power-monitor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var config = Path.Combine(directory, "Config.wtf");
        var snapshot = Path.Combine(directory, "grid.wtf");
        File.WriteAllText(config, "SET graphicsQuality \"6\"\nSET maxFPS \"200\"\n");

        WowConfigPatcher.Snapshot(config, snapshot);
        WowConfigPatcher.ApplyCvars(config, WowConfigPatcher.BackupCvars);
        Assert.Contains("SET graphicsQuality \"1\"", File.ReadAllText(config));

        WowConfigPatcher.RestoreSnapshot(config, snapshot);
        Assert.Equal("SET graphicsQuality \"6\"\nSET maxFPS \"200\"\n", File.ReadAllText(config));
    }

    [Fact]
    public void Catalog_is_the_list_of_game_presets()
    {
        Assert.Equal(["wow-forever"], GameProfiles.All.Select(game => game.Id).ToArray());
    }

    [Fact]
    public void WowForever_backup_then_grid_restores_the_original_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), "power-monitor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var config = Path.Combine(directory, "Config.wtf");
        File.WriteAllText(config, "SET graphicsQuality \"6\"\n");
        var game = new WowForeverProfile(Path.Combine(directory, "snapshot.wtf"));
        var settings = new AppSettings { WowForeverConfigPath = config };

        Assert.Contains("low-power", game.Apply(onBackup: true, settings));
        Assert.Contains("SET graphicsQuality \"1\"", File.ReadAllText(config));

        Assert.Contains("quality", game.Apply(onBackup: false, settings));
        Assert.Contains("SET graphicsQuality \"6\"", File.ReadAllText(config));
    }
}
