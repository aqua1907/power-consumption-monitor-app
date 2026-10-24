using LibreHardwareMonitor.Hardware;

namespace PowerMonitor.Pc;

internal sealed class PcPowerReader : IDisposable
{
    private readonly object _gate = new();
    private Computer? _computer;
    private bool _openFailed;

    public PcPowerReading Read(int baselineWatts)
    {
        lock (_gate)
        {
            try
            {
                EnsureOpen();
                if (_computer is null)
                    return PcPowerReading.Unavailable(baselineWatts);

                double? cpu = null;
                double? gpu = null;
                foreach (var hardware in _computer.Hardware)
                {
                    try
                    {
                        hardware.Update();
                        if (hardware.HardwareType == HardwareType.Cpu)
                            cpu = Pick(hardware, cpu, IsCpuPackage);
                        else if (IsGpu(hardware.HardwareType))
                            gpu = Pick(hardware, gpu, IsGpuTotal);
                    }
                    catch
                    {
                        // One sensor source can fail without hiding the others.
                    }
                }

                return new PcPowerReading(cpu, gpu, baselineWatts);
            }
            catch
            {
                return PcPowerReading.Unavailable(baselineWatts);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _computer?.Close();
            _computer = null;
        }
    }

    private void EnsureOpen()
    {
        if (_computer is not null || _openFailed)
            return;

        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true
            };
            computer.Open();
            _computer = computer;
        }
        catch
        {
            _openFailed = true;
            _computer = null;
        }
    }

    private static double? Pick(IHardware hardware, double? current, Func<string, bool> match)
    {
        var chosen = Choose(hardware, match);
        if (chosen is null)
        {
            foreach (var sub in hardware.SubHardware)
            {
                try
                {
                    sub.Update();
                }
                catch
                {
                    continue;
                }

                chosen = Choose(sub, match);
                if (chosen is not null)
                    break;
            }
        }

        return chosen is null ? current : (current ?? 0) + chosen.Value;
    }

    private static float? Choose(IHardware hardware, Func<string, bool> match)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Power || sensor.Value is not float value || value < 0)
                continue;
            if (match(sensor.Name ?? ""))
                return value;
        }

        return null;
    }

    private static bool IsCpuPackage(string name) =>
        name.Contains("Package", StringComparison.OrdinalIgnoreCase);

    private static bool IsGpuTotal(string name) =>
        name.Contains("Package", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Board", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Total", StringComparison.OrdinalIgnoreCase)
        || name.Equals("GPU Power", StringComparison.OrdinalIgnoreCase);

    private static bool IsGpu(HardwareType type) =>
        type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
}
