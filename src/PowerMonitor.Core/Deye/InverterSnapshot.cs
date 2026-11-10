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
    DateTimeOffset ReadAt,
    double BatteryCurrentAmps = 0,
    double GridVoltageVolts = 0,
    double GridCurrentAmps = 0,
    InverterEnergy? Energy = null)
{
    public BatteryFlow BatteryFlow => BatteryPowerWatts switch
    {
        > 50 => BatteryFlow.Discharging,
        < -50 => BatteryFlow.Charging,
        _ => BatteryFlow.Idle
    };
}
