# Calibration and game-specific setup

Deadeye separates detected devices, configured software and demonstrated gameplay.
A connected P2 device is necessary for a physical two-player test. A second player
supported by the game does not by itself prove independent gun inputs.

## RS3 screen calibration

Open **My Guns → Setup & checks**, prepare the RS3 calibration module, then start
calibration for the selected player at the physical Windows screen. Connect all
four IR emitters and the 24 V supply. Stay in the position you will play from.
Pull and release the trigger to start, then shoot the center, upper-left,
lower-right and upper-right targets once each. Calibration uses 16:9; the normal
4:3 game mode is applied separately when needed.

Only input from the selected gun advances the sequence. The grip button, Escape
or the visible Cancel button aborts an unfinished sequence. Calibration is blocked
inside Remote Desktop because its viewport cannot establish physical aim.

The module is downloaded separately from the manufacturer's calibration package
and checked against reviewed SHA-256 hashes. The vendor transport does not report
whether the firmware accepted the calibration. Deadeye therefore reports commands
sent and asks for a separate aim test; it never infers accuracy from a download or
successful native call. [Manufacturer setup files](https://drive.google.com/drive/folders/1UPyF7ziHxFdJ39c4H9DZsvgDMJ4A5jnv).

## Dolphin / Wii

On launch, the actual ISO/WBFS disc ID selects the appropriate USA profile from
[ProfgLX's accuracy INIs](https://github.com/ProfgLX/Dolphin-Lightguns-Accuracy-Inis).
The profiles are separately downloaded GPL-3.0 assets with their license preserved.
Existing unrelated Dolphin settings are retained and changed INIs are backed up.
Unsupported disc IDs retain their existing configuration.

With two supported connected guns and a matching disc profile, Deadeye creates
two independent inputs through Dolphin's native DSU backend. A loopback-only bridge
carries each gun's absolute aim and buttons into its own emulated Wii Remote.
It requires no virtual-controller driver and retains the guns' mouse/keyboard mode.
The per-player profiles and startup legend use the actual connected devices.
A single RS3 gun uses the existing mouse/keyboard profile. XGunner, Vyper and fully
captured Sinden assignments use the independent bridge, including supported mixed
pairs. Vyper menu controls must be captured; Sinden requires its non-trigger
controls to be captured before it qualifies for this path. Unknown disc IDs
retain their existing settings rather than receiving a guessed two-player profile.
The bridge's transport and separate player subscriptions have automated checks;
physical aiming and simultaneous gameplay still require a test at the monitor.
The [Dolphin guide](https://www.sindenwiki.org/wiki/Dolphin) and
[DemulShooter's Dolphin guide](https://github.com/argonlefou/DemulShooter/wiki/Dolphin)
include version-specific alternatives; an old Dolphin 5.0 hook is not presumed to
work with a current Dolphin build.

### Dead Space Extraction

Use the standard Wii Remote with a Nunchuk. The generated default RS3 mapping is:

| RS3 control | Emulated action |
| --- | --- |
| Trigger | B / shoot |
| Magazine button | A / interact and kinesis |
| Grip button | Nunchuk Z / reload |
| Side button | Nunchuk C / stasis |
| Start | Plus / pause |
| Coin, brief hold | Tilt / alternate fire |
| Stick click | Wii Remote shake / glow worm |
| Grip + side | Nunchuk shake / melee |
| Stick directions | Nunchuk stick / weapon selection, using the title accuracy profile's orientation |

Learned physical button assignments are used when available. Avoid long Coin or
stick holds: the RS3 firmware reserves these for Escape and LED functions. This
mapping needs a physical tutorial/gameplay test, including aiming, held buttons
and simultaneous actions, before the game is marked tested.

### The same Wii actions on other gun families

The supported Dolphin profile resolves the selected gun's physical controls before
building its player channel. For Dead Space Extraction with default actions:

| Action | XGunner | Blamcon Vyper | Captured Sinden |
| --- | --- | --- | --- |
| Shoot | Trigger | Trigger | Trigger |
| Reload | Right mouse button | Magazine pull | Pump |
| Interact / kinesis | Middle mouse button | A | Rear-right side button |
| Stasis | Space | B | Front-right side button |
| Pause | Start | Captured Start | Front-left side button |
| Alternate fire | Coin | Captured Select | Rear-left side button |
| Shake / glow worm | Opposite-side Enter | A+B unless a selector input is captured | Rear-right + front-right side buttons |
| Melee | Right mouse + Space | Magazine pull + B | Pump + front-right side button |
| Weapon selection | WASD stick | Captured stick directions | Captured D-pad |

The title's accuracy profile determines the weapon-axis orientation. Custom action
bindings can change these routes; the startup legend reads the resulting profile.
For Vyper, a captured selector can provide the separate shake/mode input instead
of the A+B combination. Each player needs their own completed capture and an
actual gameplay check at the physical screen.

## RPCS3 / PlayStation Move

Recent official RPCS3 builds support separate Raw Mouse inputs for PS Move.
Deadeye configures `raw_mouse.yml`, `gem_mouse.yml` and the existing title-specific
configuration for connected guns. It retains unrelated settings and backs up
changed files. Older builds without this input backend retain their settings.
This setup applies to standalone RPCS3; it does not replace TeknoParrot's bundled
arcade RPCS3. Complete the game's calibration for each player before playing.

## Launch checks in the frontend

Game Details shows the most recent native launch check, whether a game window was
observed, and whether its owned processes ended cleanly. A successful launch check
is not a full playthrough or proof of physical gun inputs. Failed launches retain
their observed error and check time instead of acquiring a gameplay-verified label.

## Other game paths

| Game / emulator | Setup behavior and remaining limitation |
| --- | --- |
| Supermodel / The Lost World | Rebuilds independent RawInput mouse and keyboard numbers from the live device list at each launch, including title overrides. The startup legend resolves those actual assignments. Physical P2 gameplay remains to test. |
| Silent Hill Arcade in TeknoParrot | Uses the existing TeknoParrot profile and its separate RawInput devices. The standalone No-Cursor/DemulShooter patch is a different launch path and must not be layered over it blindly. [Guide](https://www.sindenwiki.org/wiki/Silent_Hill_Arcade). |
| The House of the Dead Remake | Requires an ArcadePlugin matching the game build. Multiplayer also needs its multiplayer input mode and DemulShooterX64 with `-target=windows -rom=hotdra`. Existing files alone do not confirm plugin mode or two-gun readiness. [Plugin source](https://github.com/argonlefou/HotdRemake_ArcadePlugin), [game guide](https://www.sindenwiki.org/wiki/The_House_of_the_Dead_Remake). |
| Blue Estate | Requires a matching unofficial 32-bit patch in the executable's directory, Raw Mode and fullscreen. Deadeye updates the VID/PID assignments for an installed patch and rejects duplicate identities. Patch installation requires source and game-build review; it is not distributed with Deadeye. Focus changes and hotplug are unsupported by the patch. [Guide](https://www.sindenwiki.org/wiki/Blue_Estate). |
| HOTD 2 Remake | Uses its own `MultiLightgunPlugin.dll`, with player assignment at its trigger-pairing screen. Do not apply the HOTD 1 `hotdra` recipe. |
| Operation Wolf Returns (non-VR) | Use the matching OperationWolf plugin from the official DemulShooter release and `DemulShooterX64 -target=windows -rom=opwolfr`. Disable obsolete competing plugins with a backup. [Helper guide](https://github.com/argonlefou/DemulShooter/wiki/Windows-games). |
| Model 2 1.1a | Use `DemulShooter.exe -target=model2 -rom=<set>`. Deadeye disables native `UseRawInput` and `DrawCross` for this helper path. Physical SERVICE calibration is still needed. [Guide](https://github.com/argonlefou/DemulShooter/wiki/Model2). |
| HOTD 2/3 classic Windows versions | Both players must use Keyboard controls. Use `hod2pc` / `hod3pc` with the 32-bit helper. Arcade Mod launchers must point at the real game executable. [Guide](https://github.com/argonlefou/DemulShooter/wiki/Windows-games). |

Game Details shows these requirements together with whether a second gun is
currently detected. A real game test remains separate from configuration status.

### PS2 limitation

The existing GunCon2 mouse path uses a shared Windows pointer. Two connected RS3
guns alone do not provide independent PS2 players. A validated per-device axis or
controller integration and each game's own calibration are still required. This
is explicitly identified in Game Details; PS2 two-gun gameplay is not marked ready.

## Installed multiplayer helpers

Before launching a configured DemulShooter helper, Deadeye merges P1/P2 RawInput
IDs from the live device list into its `config.ini`. Unassigned P1–P4 channels
receive unmatched nonempty identifiers: empty names can match a nameless RDP
mouse in the helper. The original INI is backed up once and unrelated settings
are retained. This configures an installed integration; it does not download
arbitrary patches or infer that the game build supports the hook.

Two local players, alternating players and linked cabinets are different game
modes. A multiplayer label does not automatically mean simultaneous independent
lightguns. In particular, Dolphin P2 and linked-cabinet games need their own
verified integration rather than a generic two-mouse patch.
