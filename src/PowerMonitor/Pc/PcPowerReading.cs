namespace PowerMonitor.Pc;

internal sealed record PcPowerReading(double? CpuWatts, double? GpuWatts, int BaselineWatts)
{
    public bool SensorsAvailable => CpuWatts is not null || GpuWatts is not null;

    public double EstimatedWatts => (CpuWatts ?? 0) + (GpuWatts ?? 0) + BaselineWatts;

    public string Summary => $"~{EstimatedWatts:0} W estimated";

    public string Detail
    {
        get
        {
            if (!SensorsAvailable)
                return $"Sensors unavailable, baseline {BaselineWatts} W.";

            var cpu = CpuWatts is null ? "CPU unavailable" : $"CPU {CpuWatts.Value:0} W";
            var gpu = GpuWatts is null ? "GPU unavailable" : $"GPU {GpuWatts.Value:0} W";
            return $"{cpu} · {gpu} · other {BaselineWatts} W";
        }
    }

    public static PcPowerReading Unavailable(int baselineWatts) => new(null, null, baselineWatts);
}
