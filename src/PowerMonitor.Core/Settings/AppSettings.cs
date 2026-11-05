namespace PowerMonitor.Core.Settings;

public sealed record AppSettings
{
    public const decimal PackCapacityKwh = 5.12m;
    public const int IdleLoadWatts = 50;
    public const int MinPollIntervalSeconds = 1;
    public const int MaxPollIntervalSeconds = 3600;

    public string LoggerHost { get; init; } = "";
    public int LoggerPort { get; init; } = 502;
    public LoggerProtocol Protocol { get; init; } = LoggerProtocol.ModbusTcp;
    public int UnitId { get; init; } = 1;
    public uint LoggerSerial { get; init; }
    public bool InvertPowerSign { get; init; }
    public int RegisterAddressOffset { get; init; }
    public int ReserveSocPercent { get; init; } = 10;
    public int LowSocAlertPercent { get; init; } = 20;
    public int PackCount { get; init; } = 1;
    public int PcBaselineWatts { get; init; } = 40;
    public int PollIntervalSeconds { get; init; } = 10;
    public bool StartWithWindows { get; init; }
    public bool NotifyOnGridLoss { get; init; } = true;
    public bool NotifyOnLowSoc { get; init; } = true;

    /// <summary>
    /// When the Deye inverter loses grid, drop display refresh / GPU power and switch every
    /// <see cref="Games.GameProfiles"/> entry to its low-power preset.
    /// </summary>
    public bool BackupPlayModeEnabled { get; init; }

    public string WowForeverConfigPath { get; init; } =
        @"D:\Games\Battle.net\World of Warcraft\_forever_\WTF\Config.wtf";

    public int BackupDisplayHz { get; init; } = 60;
    public int GridDisplayHz { get; init; } = 240;
    public bool BackupGpuPowerEnabled { get; init; } = true;
    public int BackupGpuPowerWatts { get; init; } = 175;
    public int GridGpuPowerWatts { get; init; } = 450;
}
