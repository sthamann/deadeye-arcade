# 0.3.9 verification

Core regression checks cover independent live helper assignments, retained custom
INI options, recoverable backups, removal of stale P2 assignments, nameless RDP
mouse isolation, duplicate device rejection, Model 2 input conflict prevention,
Blue Estate VID/PID rejection, and separation of HOTD 1/2 Remake recipes.

147 core checks pass, and both UI compilation and Windows cross-compilation
succeed. The Windows release workflow additionally builds the installer.

Live Windows checks confirm that HOTD 1 Remake reaches its ArcadePlugin attract
menu and opens the DemulShooter shared-memory connection. The compatible
April 2022 plugin loads; the alternate July build disables itself as designed.
DemulShooter still reports an unknown assembly hash for this installation,
so physical aiming and button behavior require a separate check.

Operation Wolf Returns starts with its current DemulShooter plugin. The helper
recognizes the game assembly and connects to the plugin TCP server. A diagnostic
restart confirms that the real P1 device is selected and disconnected P2/P3/P4
channels retain unmatched identifiers.

Blue Estate patch activation remains pending source approval and an exact-build
check. Finding patch files does not establish that the game has been patched.

A successful game start is not proof of independent two-gun aiming or recoil.
Physical P2 gameplay and per-game calibration remain separate checks.
