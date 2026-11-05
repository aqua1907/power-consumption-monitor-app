using System.Diagnostics;
using System.Text;

namespace PowerMonitor.BackupMode;

/// <summary>
/// Sets the NVIDIA power limit via a one-time elevated scheduled task.
/// Direct nvidia-smi -pl calls need admin; the helper avoids a UAC prompt on every outage.
/// </summary>
internal static class GpuPowerLimit
{
    public const string TaskName = "PowerMonitorGpuPowerLimit";

    public static string WattsPath => Path.Combine(DataDirectory, "gpu-power-watts.txt");
    public static string ScriptPath => Path.Combine(DataDirectory, "apply-gpu-power-limit.ps1");

    private static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PowerMonitor");

    public static bool HelperInstalled()
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(start);
            if (process is null)
                return false;
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void WriteHelperScript()
    {
        Directory.CreateDirectory(DataDirectory);
        var script = """
            $ErrorActionPreference = 'Stop'
            $wattsPath = Join-Path $env:LOCALAPPDATA 'PowerMonitor\gpu-power-watts.txt'
            if (-not (Test-Path $wattsPath)) { throw "Missing $wattsPath" }
            $watts = (Get-Content -Path $wattsPath -Raw).Trim()
            if ($watts -notmatch '^\d+$') { throw "Bad watts value: $watts" }
            $smi = Get-Command nvidia-smi.exe -ErrorAction SilentlyContinue
            if (-not $smi) {
              $fallback = Join-Path ${env:ProgramFiles} 'NVIDIA Corporation\NVSMI\nvidia-smi.exe'
              if (Test-Path $fallback) { $smi = Get-Item $fallback }
            }
            if (-not $smi) { throw 'nvidia-smi.exe not found' }
            & $smi.Source -pl $watts
            if ($LASTEXITCODE -ne 0) { throw "nvidia-smi exited $LASTEXITCODE" }
            """;
        File.WriteAllText(ScriptPath, script, Encoding.UTF8);
    }

    /// <summary>
    /// Creates or replaces the elevated scheduled task. Must run from an elevated process.
    /// </summary>
    public static void InstallHelperElevated()
    {
        WriteHelperScript();
        // ONCE + far-future start keeps a dormant task that we trigger with /Run.
        var tr =
            $"powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ScriptPath}\"";
        var args =
            $"/Create /TN \"{TaskName}\" /TR \"{tr}\" /SC ONCE /ST 00:00 /SD 01/01/2099 /RL HIGHEST /F";
        var start = new ProcessStartInfo
        {
            FileName = "schtasks",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start schtasks.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(15000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Could not install GPU helper: {stderr}{stdout}".Trim());
    }

    public static void StartElevatedInstaller()
    {
        WriteHelperScript();
        var exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not locate PowerMonitor.exe.");
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = "--install-gpu-power-helper",
            UseShellExecute = true,
            Verb = "runas"
        });
    }

    public static string? TryApply(int watts)
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(WattsPath, watts.ToString());

        if (HelperInstalled())
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/Run /TN \"{TaskName}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var process = Process.Start(start);
                if (process is null)
                    return "Could not start the GPU power helper.";
                process.WaitForExit(10000);
                if (process.ExitCode != 0)
                {
                    var err = process.StandardError.ReadToEnd();
                    return $"GPU power helper failed: {err}".Trim();
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // Best effort without the helper; usually fails without admin.
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = $"-pl {watts}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(start);
            if (process is null)
                return "nvidia-smi was not found.";
            var stderr = process.StandardError.ReadToEnd();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10000);
            if (process.ExitCode == 0)
                return null;

            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (detail.Contains("Insufficient Permissions", StringComparison.OrdinalIgnoreCase))
            {
                return "GPU power limit needs the one-time helper (Settings → Install GPU power helper).";
            }

            return $"nvidia-smi failed: {detail}".Trim();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
