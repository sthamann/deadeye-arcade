# 0.3.9 verification

Core regression checks cover independent live helper assignments, retained custom
INI options, recoverable backups, removal of stale P2 assignments, nameless RDP
mouse isolation, duplicate device rejection, Model 2 input conflict prevention,
Blue Estate VID/PID rejection, and separation of HOTD 1/2 Remake recipes.

147 core checks pass, and both UI compilation and Windows cross-compilation
succeed. The Windows release workflow successfully built and published the
installer and portable package. The published installer was downloaded,
checked against the release SHA-256 manifest, and installed on Windows.
Its product version resolves to `0.3.9+f2d1b58ff1266749b8e12d98e0b87bbf4243e2d5`.
The library file hash remained unchanged during installation.

The installed frontend opens with game preview media and the live P1 status.
Operation Wolf was launched through the frontend, the F10 overlay was opened,
and End Game returned to the library. Subsequent process and helper-log checks
confirmed that the game and DemulShooter exited. The visible Close App control
also closed the frontend. Holding a physical trigger for ten seconds still
requires a local hardware test. The overlay currently has no confirmed
per-game button map for Operation Wolf.

Live Windows checks confirm that HOTD 1 Remake reaches its ArcadePlugin attract
menu and opens the DemulShooter shared-memory connection. The compatible
April 2022 plugin loads; the alternate July build disables itself as designed.
DemulShooter still reports an unknown assembly hash for this installation,
so physical aiming and button behavior require a separate check.

Operation Wolf Returns starts with its current DemulShooter plugin. The helper
recognizes the game assembly and connects to the plugin TCP server. A diagnostic
restart confirms that the real P1 device is selected and disconnected P2/P3/P4
channels retain unmatched identifiers.

Model 2 1.1a is recognized by DemulShooter, and its P1/P2 axis hooks install.
An initial game-loading attempt crashed with Windows exception `0xc0000005`.
A subsequent windowed Virtua Cop run with the documented `ForceManaged=1`
renderer option reached animated attract graphics and remained running after
DemulShooter attached. The live P1 device was selected; absent channels retained
unmatched identifiers. Automatic command-line game loading is still unresolved,
so this is not counted as successful frontend launch or two-gun gameplay.

HOTD 3 launches after correcting its game path, but its executable hash is
unknown to the helper. Its input compatibility remains unverified.

Blue Estate patch activation remains pending source approval and an exact-build
check. Finding patch files does not establish that the game has been patched.

A successful game start is not proof of independent two-gun aiming or recoil.
Physical P2 gameplay and per-game calibration remain separate checks.
