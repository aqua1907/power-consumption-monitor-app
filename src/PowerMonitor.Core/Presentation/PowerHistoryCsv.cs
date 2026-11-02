using System.Globalization;

namespace PowerMonitor.Core.Presentation;

/// <summary>
/// Exports retained history samples to a CSV file for offline analysis.
/// </summary>
public static class PowerHistoryCsv
{
    public static string SuggestedFileName(DateTimeOffset? at = null) =>
        $"power-history-{(at ?? DateTimeOffset.Now):yyyyMMdd-HHmmss}";

    /// <returns>The path the CSV was written to.</returns>
    public static string Write(IReadOnlyList<PowerSample> samples, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var writer = new StreamWriter(path, append: false);
        writer.WriteLine("Time,BatteryWatts,LoadWatts");
        foreach (var sample in samples)
        {
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{sample.At:O},{sample.BatteryWatts},{sample.LoadWatts}"));
        }

        return path;
    }
}
