Deadeye Arcade 0.3.18 improves gun input, game startup and recovery.

- Prevents overlapping launches and tracks owned child processes, including games that use a separate renderer executable. Ending or restarting a session releases its game processes and helpers.
- Restores P2 trigger navigation in Gun Studio and calibration. Device discovery no longer unregisters the frontend's active input listener.
- Uses a compact, scrollable startup legend and game menu with monitor-aware placement.
- Adds separate P1/P2 Dolphin inputs through its native DSU backend for supported RS3 title profiles, with no virtual-controller driver. Physical two-player gameplay still needs testing.
- Configures supported RetroArch lightgun devices, physical mouse indexes, title options and protected emulator hotkeys.
- Configures separate Flycast raw devices and recent standalone RPCS3 Raw Mouse / PS Move bindings.
- Applies installed TeknoParrot vendor metadata while retaining player inputs and game paths; fixes supported legacy path and display settings.
- Handles temporary RS3 serial-port failures without blocking otherwise available USB input.

Launch checks confirm startup, a detected window, parsed bindings and session cleanup.
They do not prove aiming, calibration, recoil, multiplayer or full-game stability.
Game libraries, input settings and startup preferences are retained.

Game Details now shows recorded native launch and cleanup results separately from
physical gameplay verification. Supermodel title overrides and stale TeknoParrot
player labels are resolved from the active configuration. RPCS3 input YAML uses
the Windows config subdirectory, including portable layouts.
