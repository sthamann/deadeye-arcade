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

RS3 P1 uses the Windows mouse plus the gun's keyboard buttons. Existing GUN4IR
P2 mappings are not treated as RS3 mappings. An independently verified DirectInput
RS3 P2 profile is still required for two players. If an RS3 P2 is connected during
Dolphin launch, Deadeye temporarily switches it into joystick mode so it cannot
move P1's shared mouse pointer; the second emulated Wii Remote stays disabled until
its independent mapping has been verified. Menu mouse mode is restored afterward.
The [Dolphin guide](https://www.sindenwiki.org/wiki/Dolphin) and
[DemulShooter's Dolphin guide](https://github.com/argonlefou/DemulShooter/wiki/Dolphin)
include version-specific alternatives; an old Dolphin 5.0 hook is not presumed to
work with a current Dolphin build.

### Dead Space Extraction P1

Use the standard Wii Remote with a Nunchuk. The generated RS3 mapping is:

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
| Stick directions | Nunchuk stick / weapon selection |

Learned physical button assignments are used when available. Avoid long Coin or
stick holds: the RS3 firmware reserves these for Escape and LED functions. This
mapping needs a physical tutorial/gameplay test, including aiming, held buttons
and simultaneous actions, before the game is marked tested.

## Other game paths

| Game / emulator | Setup behavior and remaining limitation |
| --- | --- |
| Supermodel / The Lost World | Rebuilds independent RawInput mouse and keyboard numbers from the live device list at each launch. Unconnected players receive no phantom joystick mapping. Physical P2 gameplay remains to test. |
| Silent Hill Arcade in TeknoParrot | Uses the existing TeknoParrot profile and its separate RawInput devices. The standalone No-Cursor/DemulShooter patch is a different launch path and must not be layered over it blindly. [Guide](https://www.sindenwiki.org/wiki/Silent_Hill_Arcade). |
| The House of the Dead Remake | Requires an ArcadePlugin matching the game build. Multiplayer also needs its multiplayer input mode and DemulShooterX64 with `-target=windows -rom=hotdra`. Existing files alone do not confirm plugin mode or two-gun readiness. [Plugin source](https://github.com/argonlefou/HotdRemake_ArcadePlugin), [game guide](https://www.sindenwiki.org/wiki/The_House_of_the_Dead_Remake). |
| Blue Estate | Requires a matching unofficial 32-bit patch in the executable's directory, Raw Mode and fullscreen. Deadeye updates the VID/PID assignments for an installed patch and rejects duplicate identities. Patch installation requires source and game-build review; it is not distributed with Deadeye. Focus changes and hotplug are unsupported by the patch. [Guide](https://www.sindenwiki.org/wiki/Blue_Estate). |
| HOTD 2 Remake | Uses its own `MultiLightgunPlugin.dll`, with player assignment at its trigger-pairing screen. Do not apply the HOTD 1 `hotdra` recipe. |
| Operation Wolf Returns (non-VR) | Use the matching OperationWolf plugin from the official DemulShooter release and `DemulShooterX64 -target=windows -rom=opwolfr`. Disable obsolete competing plugins with a backup. [Helper guide](https://github.com/argonlefou/DemulShooter/wiki/Windows-games). |
| Model 2 1.1a | Use `DemulShooter.exe -target=model2 -rom=<set>`. Deadeye disables native `UseRawInput` and `DrawCross` for this helper path. Physical SERVICE calibration is still needed. [Guide](https://github.com/argonlefou/DemulShooter/wiki/Model2). |
| HOTD 2/3 classic Windows versions | Both players must use Keyboard controls. Use `hod2pc` / `hod3pc` with the 32-bit helper. Arcade Mod launchers must point at the real game executable. [Guide](https://github.com/argonlefou/DemulShooter/wiki/Windows-games). |

Game Details shows these requirements together with whether a second gun is
currently detected. A real game test remains separate from configuration status.

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
