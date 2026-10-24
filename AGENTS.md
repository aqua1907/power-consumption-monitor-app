## Learned User Preferences

- Run the monitor from the Windows notification area and read apartment backup status from the Deye logger on the local network.
- Prefer a compact, resizable, theme-aware glass flyout with a clear live view of charging, discharging, and power flow. The earlier full-size dashboard was too large.
- Draw every window at the DPI of the monitor it is on, including this 4K display at 150% scaling, and rescale when it moves to another screen.

## Learned Workspace Facts

- WinUI 3 tray app lives in `src\PowerMonitor` (`dotnet run --project src\PowerMonitor`). Left-click toggles a compact flyout above the clock; Esc or a click outside hides it unless it is pinned, and it stays out of Alt+Tab. Right-click opens Settings or Exit. A second launch focuses the existing instance. Settings and Find logger are separate windows.
- The station is a Deye SUN-6K-SG05LP1-EU-AM2-P inverter with one SE-G5.1 Pro-B pack (5.12 kWh). Local reads use Modbus TCP on port 502, unit id 1, after logger firmware 1.7.x with Modbus TCP enabled on Link 2 while Deye Cloud stays on Link 1. Older sticks use Solarman V5 on port 8899 plus the serial printed on the stick. A wrong state of charge means switch the address offset; reversed charge and discharge means flip the power sign.
- Backup time uses the 5.12 kWh pack, a 10% reserve, and the inverter apartment load. While the grid is up, that figure is the time that would remain if the grid dropped now. Loads under 50 W show Idle. This PC's watts are CPU plus GPU plus a configurable 40 W baseline and are shown separately from the backup timer. Notifications fire when the grid drops and when the battery crosses 20%.
- The loop reads immediately at startup and after a settings save, then waits about 10 seconds before the next read. History keeps about 30 minutes (180 samples). At 640 px wide or more, the history chart sits in a second column.
- PC power is read with LibreHardwareMonitor: PawnIO for AMD package power and NVIDIA libraries for GPU power. Battery and flow animations stop while the dashboard is hidden.
