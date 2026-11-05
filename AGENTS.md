## Learned User Preferences

- Run the monitor from the Windows notification area and read apartment backup status from the Deye logger on the local network.
- Prefer a compact, resizable, theme-aware glass flyout with a clear live view of charging, discharging, and power flow. The earlier full-size dashboard was too large.
- Draw every window at the DPI of the monitor it is on, including this 4K display at 150% scaling, and rescale when it moves to another screen.
- Export battery history through a Save As picker.
- Leave out solar/PV tracking and daily or weekly energy and cost summaries.

## Learned Workspace Facts

- WinUI 3 tray app lives in `src\PowerMonitor` (`dotnet run --project src\PowerMonitor`). Left-click toggles a compact flyout above the clock; Esc or a click outside hides it unless it is pinned, and it stays out of Alt+Tab. Right-click opens History (outage list and CSV export), Settings, or Exit. A second launch focuses the existing instance. Settings, Find logger, and History are separate windows.
- The station is a Deye SUN-6K-SG05LP1-EU-AM2-P inverter with one SE-G5.1 Pro-B pack (5.12 kWh). Local reads use Modbus TCP on port 502, unit id 1, after logger firmware 1.7.x with Modbus TCP enabled on Link 2 while Deye Cloud stays on Link 1. Older sticks use Solarman V5 on port 8899 plus the serial printed on the stick. A wrong state of charge means switch the address offset; reversed charge and discharge means flip the power sign.
- Backup time uses the 5.12 kWh pack, a 10% reserve, and the inverter apartment load. While the grid is up, that figure is the time that would remain if the grid dropped now. Loads under 50 W show Idle. This PC's watts are CPU plus GPU plus a configurable 40 W baseline and are shown separately from the backup timer. Notifications fire when the grid drops and when the battery crosses the low-battery setting (default 20%). That threshold also sets the tray icon and dashboard critical color.
- The loop reads immediately at startup and after a settings save, then waits the Settings "Update every (seconds)" interval (default 10, clamped to 1–3600) before the next read. Battery history is kept for 24 hours in memory and written to `%AppData%\PowerMonitor\history.json` so the chart survives a restart. Chart ranges are 30m, 1h, 4h, 12h, and 24h, with time and watt axis labels and a hover or tap crosshair. At 640 px wide or more, the history chart sits in a second column.
- PC power is read with LibreHardwareMonitor: PawnIO for AMD package power and NVIDIA libraries for GPU power. Battery and flow animations stop while the dashboard is hidden.
- The repo is on Git branch `main`, published as a public GitHub repo (pushed with the `gh` CLI). Pushing a `v*` tag runs GitHub Actions and publishes a self-contained win-x64 zip. `README.md` covers purpose and how to run, with a live flyout screenshot at `docs/flyout.png`. `.gitignore` excludes `bin/`, `obj/`, `.vs/`, `*.user`, `TestResults/`, and `.cursor/`. Settings stay at `%AppData%\PowerMonitor\settings.json`, outages at `%AppData%\PowerMonitor\outages.json`, and startup errors at `%LocalAppData%\PowerMonitor\startup-error.txt`. Keep real logger serials out of commits.
- The LAN is treated as untrusted. UDP discovery keeps only replies whose advertised IP matches the sender, capped at 256. `RegisterDecoder` rejects SOC over 100, battery over 70 V, power over 20 kW, and grid flags other than 0/1. `SocJumpGuard` holds back SOC jumps larger than 5% plus 3% per minute until a second reading confirms. Any failed read turns the tray icon into a red "!" with a "Stale since HH:mm" tooltip.
- Settings can start the app with Windows. The option is off by default and, when enabled, writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DeyePowerMonitor` to the running executable.

## Working Notes

- Shell is PowerShell on Windows. Use `Get-ChildItem`, `Select-String`, and `Stop-Process`; `grep`, `find`, and `cat` are not available. Globbing all of `C:\Users\Ivan` times out, so scope searches to the repo or use `Get-ChildItem -Recurse -Depth`.
- Unit tests: `dotnet test tests\PowerMonitor.Tests\PowerMonitor.Tests.csproj --nologo --filter "FullyQualifiedName~<Name>"`. Tests are xUnit `[Fact]` and write temp files under `Path.GetTempPath()\power-monitor-tests\<guid>`.
- App build: `dotnet build src\PowerMonitor\PowerMonitor.csproj --nologo`. Stop the running `PowerMonitor` process first, or the build fails with MSB3027 on `PowerMonitor.Core.dll`. Restart with `dotnet run --project src\PowerMonitor\PowerMonitor.csproj --no-build`.
- `nvidia-smi -pl` needs admin. The app uses the scheduled task `PowerMonitorGpuPowerLimit` for it.
- Stage by path (`git add -- <paths>`). `AGENTS.md` and `.idea/` can hold unrelated edits. Before committing, scan the staged diff for logger serials, tokens, and WoW account IDs.
- Game root is `D:\Games\Battle.net\World of Warcraft`. Forever runs as process `Wow` from `_forever_`; Midnight is `_retail_`. Both share one account folder, so keep `WTF` contents out of commits.
- The WoW client rewrites `Config.wtf` and cache files on exit and logout. Edit them only while `Wow` is closed, or re-apply after it exits.
- The Power Status addon source is `addons\PowerStatus`. Copy it to `_forever_\Interface\AddOns\PowerStatus` after edits. The addon reads `status.lua` only on login or `/reload`. WoW loads only TGA textures, so PNG icons render blank; TGA needs bottom-left origin and 8 alpha bits. The TOC `Interface: 16001` is unverified against the client build.
- `WowAddonChannel` sends readings to Forever as Ctrl+Shift+F6/F7/F8 key packets and is not yet tested in-game. `WowStatusBits` (C#) and the decoder in `PowerStatus.lua` must match bit for bit; change both together.
- New `AppSettings` properties need defaults and a line in the `SettingsStoreTests` round-trip.
