Deadeye Arcade 0.3.19 keeps game overlays connected to the active game window.

Some games replace their splash window with another renderer window after startup.
Deadeye now reacquires a stable, visible renderer from the owned game session when
the previous handle is gone. Overlay recovery and native window checks therefore
use the current window. The startup control legend still appears only once.

This update includes the input, session cleanup, Dolphin, RetroArch, Flycast,
RPCS3 and TeknoParrot setup improvements from 0.3.18.

Launch checks confirm startup, a detected window, parsed bindings and session cleanup.
They do not prove aiming, calibration, recoil, multiplayer or full-game stability.
Game libraries, input settings and startup preferences are retained.
