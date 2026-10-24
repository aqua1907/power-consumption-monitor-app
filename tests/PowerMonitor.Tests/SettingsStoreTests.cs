using PowerMonitor.Core.Settings;

namespace PowerMonitor.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Round_trips_the_settings_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "power-monitor-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var settings = new AppSettings
        {
            LoggerHost = "192.168.1.40",
            LoggerPort = 8899,
            Protocol = LoggerProtocol.SolarmanV5,
            LoggerSerial = 1000000001,
            InvertPowerSign = true,
            RegisterAddressOffset = 1,
            ReserveSocPercent = 15,
            PackCount = 2,
            PcBaselineWatts = 55,
            PollIntervalSeconds = 15,
            StartWithWindows = true,
            NotifyOnGridLoss = false
        };

        SettingsStore.Save(settings, path);
        var loaded = SettingsStore.Load(path);

        Assert.Equal(settings, loaded);
        Assert.Contains("\"protocol\": \"SolarmanV5\"", File.ReadAllText(path));
    }
}
