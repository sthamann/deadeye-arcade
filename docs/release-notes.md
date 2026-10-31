Open Deadeye without waiting for a full-library repair.

- Show the saved dependency and repair reports at startup instead of hashing and repairing every game before enabling Launch.
- Repair and validate the selected game immediately before each launch.
- Keep the full-library check available through the explicit check and repair action.
- Show a selected game's missing runtime directly, without starting another full-library scan.

Game launching and gun navigation fixes.

- Let every installed game reach native automatic repair and current-file validation, including entries with an old incomplete setup status.
- Keep aim and button tests optional. Button testing starts only when selected, and launch preparation clears test and learning modes.
- Let informational status messages pass pointer input through to the controls beneath them.
- Release stale absolute aiming coordinates when a gun changes to relative mouse movement, while keeping independent aim for button-only packets.
- Record a bounded coordinate sample during the aim test so Windows pointer positions, raw gun coordinates and display scaling can be compared when diagnosing tracking issues.

Existing libraries, gun assignments and calibration data are preserved. Physical aiming still needs a test at the connected display.
