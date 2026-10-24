namespace PowerMonitor.Core.Settings;

public sealed record AppSettings
{
    public const decimal PackCapacityKwh = 5.12m;
    public const int IdleLoadWatts = 50;
    public const int LowSocPercent = 20;
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
    public int PackCount { get; init; } = 1;
    public int PcBaselineWatts { get; init; } = 40;
    public int PollIntervalSeconds { get; init; } = 10;
    public bool StartWithWindows { get; init; }
    public bool NotifyOnGridLoss { get; init; } = true;
    public bool NotifyOnLowSoc { get; init; } = true;
}
