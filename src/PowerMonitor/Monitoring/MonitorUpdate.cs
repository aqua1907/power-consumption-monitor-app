using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Estimate;
using PowerMonitor.Pc;

namespace PowerMonitor.Monitoring;

internal sealed record MonitorUpdate(
    InverterSnapshot? Inverter,
    BackupEstimate? Backup,
    PcPowerReading Pc,
    string? Error,
    DateTimeOffset AttemptedAt);
