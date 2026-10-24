using PowerMonitor.Core.Settings;

namespace PowerMonitor.Core.Deye;

public static class DeyeClient
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<InverterSnapshot> ReadAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Validate(settings);
        if (!await Gate.WaitAsync(TimeSpan.FromSeconds(8), cancellationToken))
            throw new TimeoutException("The logger is busy with another read.");

        try
        {
            var start = checked((ushort)(DeyeRegisters.BlockStart + settings.RegisterAddressOffset));
            IRegisterReader reader = settings.Protocol == LoggerProtocol.SolarmanV5
                ? new SolarmanV5RegisterReader(settings.LoggerHost.Trim(), settings.LoggerPort, settings.LoggerSerial)
                : new ModbusTcpRegisterReader(settings.LoggerHost.Trim(), settings.LoggerPort);

            var words = await reader.ReadHoldingRegistersAsync((byte)settings.UnitId, start, DeyeRegisters.BlockCount, cancellationToken);
            return RegisterDecoder.Decode(words, settings.InvertPowerSign, DateTimeOffset.Now);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static void Validate(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.LoggerHost))
            throw new InvalidOperationException("Enter the logger IP address in Settings.");
        if (settings.LoggerPort is < 1 or > 65535)
            throw new InvalidOperationException("The logger port must be between 1 and 65535.");
        if (settings.UnitId is < 1 or > 247)
            throw new InvalidOperationException("The Modbus unit id must be between 1 and 247.");
        if (settings.RegisterAddressOffset is < 0 or > 1)
            throw new InvalidOperationException("The register offset must be 0 or 1.");
        if (settings.ReserveSocPercent is < 0 or > 90)
            throw new InvalidOperationException("Reserve state of charge must be between 0 and 90%.");
        if (settings.PackCount is < 1 or > 64)
            throw new InvalidOperationException("Battery pack count must be between 1 and 64.");
        if (settings.PcBaselineWatts is < 0 or > 2000)
            throw new InvalidOperationException("The PC baseline must be between 0 and 2000 W.");
        if (settings.Protocol == LoggerProtocol.SolarmanV5 && settings.LoggerSerial == 0)
            throw new InvalidOperationException("Enter the logger serial number from the stick label.");
    }
}
