using PowerMonitor.Core.Presentation;

namespace PowerMonitor.Core.Wow;

public static class WowStatusFeed
{
    public static string? PathFor(string? wowConfigPath)
    {
        if (string.IsNullOrWhiteSpace(wowConfigPath))
            return null;

        var wtf = Path.GetDirectoryName(wowConfigPath);
        var root = string.IsNullOrEmpty(wtf) ? null : Path.GetDirectoryName(wtf);
        if (string.IsNullOrEmpty(root))
            return null;

        return Path.Combine(root, "Interface", "AddOns", "PowerStatus", "status.lua");
    }

    public static string Render(DashboardState state, int pcWatts, bool lowBattery)
    {
        var load = state.HasData ? DashboardPresentation.FormatPower(state.LoadPowerWatts) : "—";
        var pc = state.HasData ? DashboardPresentation.FormatPower(pcWatts) : "—";
        return string.Join('\n',
            "-- Written by PowerMonitor. WoW reads this when the addon loads.",
            "PowerStatus_Feed = {",
            $"  hasData = {(state.HasData ? "true" : "false")},",
            $"  soc = {state.SocPercent},",
            $"  low = {(lowBattery ? "true" : "false")},",
            $"  grid = {(state.GridConnected ? "true" : "false")},",
            $"  summary = {Lua(state.Summary)},",
            $"  flow = {Lua(state.FlowSummary)},",
            $"  backup = {Lua(state.Runtime)},",
            $"  load = {Lua(load)},",
            $"  pc = {Lua(pc)},",
            $"  updated = {Lua(state.Updated)},",
            $"  error = {Lua(state.Error)},",
            "}",
            "");
    }

    public static void Write(string path, DashboardState state, int pcWatts, bool lowBattery)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, Render(state, pcWatts, lowBattery));
        File.Move(temporary, path, overwrite: true);
    }

    private static string Lua(string? value)
    {
        var text = value ?? "";
        return "\"" + text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }
}
