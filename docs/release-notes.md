Game launching and gun navigation fixes.

- Let every installed game reach native automatic repair and current-file validation, including entries with an old incomplete setup status.
- Keep aim and button tests optional. Button testing starts only when selected, and launch preparation clears test and learning modes.
- Let informational status messages pass pointer input through to the controls beneath them.
- Release stale absolute aiming coordinates when a gun changes to relative mouse movement, while keeping independent aim for button-only packets.
- Record a bounded coordinate sample during the aim test so Windows pointer positions, raw gun coordinates and display scaling can be compared when diagnosing tracking issues.

Existing libraries, gun assignments and calibration data are preserved. Physical aiming still needs a test at the connected display.
