# Deadeye Arcade 0.3.30

Hold a gun's D-pad or stick direction to browse games and scroll. Navigation repeats independently of firmware keyboard repeat, stops on release and clears during session or device changes. The focused game stays visible; selecting it returns to the game preview. Captured direction controls update both the physical display and menu actions.

The player selector now distinguishes playing P1/P2 guns from standby devices, shows receiver ports in the header and supports sitting out. A per-gun guide explains the available menu controls.

Software preparation runs automatically for connected gun families, displays progress and retries temporary failures without re-scanning USB or restarting the app. RS3 calibration support, XGunner configuration software and Sinden runtime preparation are included; Blamcon ARC installation opens through Steam when needed. Steam authentication, vendor confirmations and physical calibration remain visible steps. Gun firmware is not flashed automatically.

A guided menu-button setup captures Start, Coin/Select and all four directions in sequence, waits for release between steps and saves the actual per-device signals. Reload cancels the sequence.
