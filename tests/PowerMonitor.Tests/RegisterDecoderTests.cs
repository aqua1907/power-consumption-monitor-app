using PowerMonitor.Core.Deye;

namespace PowerMonitor.Tests;

public class RegisterDecoderTests
{
    [Fact]
    public void Decodes_single_phase_hybrid_block()
    {
        var snapshot = RegisterDecoder.Decode(Sample(), invertPowerSign: false, new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(26, DeyeRegisters.BlockCount);
        Assert.Equal(72, snapshot.SocPercent);
        Assert.Equal(51.20, snapshot.BatteryVoltageVolts, 3);
        Assert.Equal(-400, snapshot.BatteryPowerWatts);
        Assert.Equal(-200, snapshot.GridPowerWatts);
        Assert.Equal(840, snapshot.LoadPowerWatts);
        Assert.True(snapshot.GridConnected);
        Assert.Equal(BatteryFlow.Charging, snapshot.BatteryFlow);
    }

    [Fact]
    public void Power_sign_flip_negates_power_registers_only()
    {
        var snapshot = RegisterDecoder.Decode(Sample(), invertPowerSign: true, DateTimeOffset.UnixEpoch);

        Assert.Equal(72, snapshot.SocPercent);
        Assert.Equal(400, snapshot.BatteryPowerWatts);
        Assert.Equal(200, snapshot.GridPowerWatts);
        Assert.Equal(-840, snapshot.LoadPowerWatts);
        Assert.Equal(BatteryFlow.Discharging, snapshot.BatteryFlow);
    }

    [Fact]
    public void Grid_flag_zero_means_the_grid_is_off()
    {
        var words = Sample();
        words[DeyeRegisters.Index(DeyeRegisters.GridConnected)] = 0;

        var snapshot = RegisterDecoder.Decode(words, invertPowerSign: false, DateTimeOffset.UnixEpoch);

        Assert.False(snapshot.GridConnected);
    }

    [Fact]
    public void Rejects_a_state_of_charge_this_inverter_cannot_report()
    {
        var words = Sample();
        words[DeyeRegisters.Index(DeyeRegisters.BatterySoc)] = 101;

        var error = Assert.Throws<InvalidOperationException>(() =>
            RegisterDecoder.Decode(words, invertPowerSign: false, DateTimeOffset.UnixEpoch));

        Assert.Contains("address offset", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ushort[] Sample()
    {
        var words = new ushort[DeyeRegisters.BlockCount];
        words[DeyeRegisters.Index(DeyeRegisters.GridPower)] = unchecked((ushort)(short)-200);
        words[DeyeRegisters.Index(DeyeRegisters.LoadPower)] = 840;
        words[DeyeRegisters.Index(DeyeRegisters.BatteryVoltage)] = 5120;
        words[DeyeRegisters.Index(DeyeRegisters.BatterySoc)] = 72;
        words[DeyeRegisters.Index(DeyeRegisters.BatteryPower)] = unchecked((ushort)(short)-400);
        words[DeyeRegisters.Index(DeyeRegisters.GridConnected)] = 1;
        return words;
    }
}
