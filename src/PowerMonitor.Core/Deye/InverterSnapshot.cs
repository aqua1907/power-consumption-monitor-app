namespace PowerMonitor.Core.Deye;

public enum BatteryFlow
{
    Charging,
    Idle,
    Discharging
}

public sealed record InverterSnapshot(
    int SocPercent,
    double BatteryVoltageVolts,
    int BatteryPowerWatts,
    int GridPowerWatts,
    int LoadPowerWatts,
    bool GridConnected,
    DateTimeOffset ReadAt)
{
    public BatteryFlow BatteryFlow => BatteryPowerWatts switch
    {
        > 50 => BatteryFlow.Discharging,
        < -50 => BatteryFlow.Charging,
        _ => BatteryFlow.Idle
    };
}
