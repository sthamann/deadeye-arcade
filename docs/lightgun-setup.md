# Supported lightguns and setup


Deadeye supports four hardware families. **My Guns** combines detection, P1/P2
selection, physical controls, calibration guidance and software preparation.

| Model | Controls represented in Deadeye | Setup integration |
| --- | --- | --- |
| **RS3 Reaper Pro** | Trigger, grip reload buttons, magazine base, Start/Coin, side button and stick with press | Windows HID/COM detection, manufacturer calibration module, aspect/offscreen settings and separate recoil/rumble tests |
| **Sinden Lightgun** | Trigger, pump, four side buttons and four D-pad directions | Manufacturer software preparation, runtime checks, guided control capture and supported game profiles |
| **X-Gunner Wireless** | Trigger, right/middle mouse inputs, Start/Coin, Space, Q/E, opposite-side Enter and directional stick | Receiver identification, manufacturer configuration tool, onboard calibration guide and local feedback tests |
| **Blamcon Vyper** | Trigger, A/B, magazine pull, Start/Select, stick directions, optional pedal and auxiliary selector input | ARC through Steam, native configuration, saved calibration profile selection and local recoil/rumble tests |

### Choose P1 and P2

Every detected device gets its own card. Select **Use for P1** or **Use for P2**;
selecting a gun already used by the other player swaps their slots. **Sit out**
removes it from active play while keeping its settings. More than two devices can
remain connected, and the two selected guns can be different models.

A game slot is separate from a receiver's hardware player number. Reassigning a
gun to game P1 does not rewrite its hardware ID. Explicit selections and per-gun
button/feedback settings survive reconnects, restarts and updates.

The header shows the detected count, active P1/P2 models and setup status. USB
presence, software preparation and a received button event have separate states.
For wireless hardware, detecting the receiver alone does not confirm a gun is
awake and sending inputs. Deadeye renews its input registrations after Windows
unlock, resume and Remote Desktop transitions. Recovery clears stale button-test
locks and pressed-button highlights so old input state cannot block the menu.

### Physical buttons and action bindings

Each family has its own illustrated controls, shared by Gun Studio and the native
startup overlay. Press a button to highlight its physical position. Opposite-side
controls and mechanical switches are labelled separately.

**Capture** learns which signal a physical control sends. **Bind** chooses what that
signal does. This separation keeps the picture accurate when you change actions.
The guided menu setup requests each control in sequence, requires release before
the next step and rejects duplicate signals. Unknown controls remain visible until
captured; restoring default actions preserves the captured physical signals.

Use **Start button test** to inspect a gun without its normal frontend menu actions
firing. Other guns can still operate the menu, and the fixed Windows exit stays
available. Shortcuts implemented inside the gun's own firmware still apply.

The D-pad or stick moves focus through games and menus; holding a direction keeps
moving and scrolls the focused item into view. Aim and trigger also work with the
fixed scrolling buttons. Selecting a cover returns to the featured game and its
launch button. The system filter opens large buttons that work with gun aiming,
trigger selection and directional navigation. Menu hints use the selected gun's
saved physical controls. Keyboard navigation follows local and Remote Desktop
mode changes without restarting the frontend.

### RS3 Reaper Pro

- **Default actions:** trigger shoots, the grip buttons reload and the magazine
  base supplies the second action. Start/Coin, side button and stick inputs follow
  the gun's actual hardware player number, including the different P2 key codes.
  Both grip reload buttons share one signal.
- **Software:** normal mouse/keyboard input uses Windows HID. Deadeye groups the
  gun's interfaces, finds its COM port and prepares the checked manufacturer
  calibration module when required.
- **Calibration:** run the integrated fullscreen four-target calibration for the
  selected gun, then the separate five-target aim test at the physical monitor.
- **Configuration and feedback:** mouse mode, 16:9/4:3, offscreen reload and bounded
  recoil/rumble test pulses. Recoil and grip rumble are separate mechanisms. The
  hardware switches and specified power supply govern mechanical recoil; Deadeye
  does not provide an undocumented continuous force slider.

### Sinden Lightgun

- **Controls:** trigger, pump, four side buttons and four D-pad directions. The
  pump is the reload control; captured side buttons supply Start, Coin and the
  additional actions used by supported profiles.
- **Guided setup:** capture all nine non-trigger controls from your own vendor
  configuration. Sinden can send different inputs on-screen and off-screen, so
  capture the intended on-screen signals. Deadeye does not guess RS3 key codes.
- **Software:** prepares the reviewed Sinden package, scans its programs for
  supported Microsoft runtimes and checks again after dependency installation.
  A fresh vendor configuration enables automatic detection/start and the border;
  vendor confirmations remain visible.
- **Tracking and feedback:** the manufacturer software handles camera tracking,
  the screen border, start/stop, calibration and supported recoil settings. Fully
  captured Sinden controls can use Deadeye's separate per-player Dolphin bridge
  for supported Wii profiles.

### X-Gunner Wireless

- **Detection:** identifies the XGUNNER receiver even when Windows supplies generic
  mouse or keyboard names. The receiver's hardware number remains separate from
  its selected game slot.
- **Default actions:** trigger shoots; right mouse or Space reloads; middle mouse
  and Q/E provide second-action inputs. Start/Enter confirm, Coin provides credit
  and the WASD directional stick drives frontend navigation. Individual signals
  remain visible and can be recaptured if the vendor mapping changes.
- **Physical layout:** Enter is a separate button on the opposite side. Power,
  vibration, rapid-fire and firmware controls are identified as hardware controls.
  The documented magazine release is mechanical; the manual does not specify an
  independent HID signal for it. Reload uses the documented button inputs.
- **Software and calibration:** prepares the reviewed manufacturer configuration
  tool and its supported runtimes. The onboard calibration guide covers IR layout
  and target order; lightgun mode and aspect selection are available in setup.
- **Feedback:** separate bounded solenoid and motor test pulses, followed by
  automatic-mode restoration. The gun's physical vibration and rapid-fire
  switches remain relevant. Reviewed TeknoParrot and Dolphin routes support mixed
  assignments with other supported families.

### Blamcon Vyper

- **Pointer setup:** detection restores Mouse/Keyboard mode before the first game.
  **Restore mouse pointer** repeats this without changing calibration or feedback.
  Match ARC Tracking to the actual emitter positions: **2 Up 2 Down** for
  four corners or two upper/two lower points; **Crosshair** only for one emitter
  in the middle of each edge. Check all four points in Live View before calibration. Buttons responding
  alone do not confirm working camera tracking.
- **Default actions:** trigger shoots, **magazine pull reloads**, A is the second
  action and B is the third action. The magazine microswitch uses the FOOT channel,
  defaulting to keyboard R in assignable mode. An optional pedal shares this
  channel; identical signals cannot be distinguished as separate controls.
- **Automatic button setup:** supported firmware 2.x supplies its actual saved
  Start, Select, directions, trigger, A/B and magazine keys over USB. Deadeye
  selects Mouse/Keyboard mode and, for initial setup, an independent magazine
  channel. Later scans preserve a saved gun profile even when that gun is sitting
  out. It reads the result back, then uses it for menu navigation, the emergency chord and game
  profiles. Custom action assignments survive
  simultaneous ARC key swaps. Changing the magazine key or returning the selector
  to recoil mode removes obsolete input assignments. Gun Studio capture remains
  available for unsupported key combinations. The two-stage mechanical trigger
  has one electrical shoot input.
- **Native settings:** mouse/keyboard mode, 16:9/4:3, offscreen-reload behavior,
  off/semi/automatic recoil, magazine/pedal mode and one of four existing
  calibration profiles. Per-game settings restore the saved menu settings on exit.
- **Software and calibration:** installs or opens official Blamcon ARC through
  Steam. Login or installation confirmation remains visible when needed. ARC
  covers calibration, camera view, IR sensitivity, smoothing, rumble strength and
  further vendor settings; tracking and calibration are stored onboard.
- **Two guns and feedback:** use separate hardware player IDs for two Vypers,
  then assign their game slots in Deadeye. Bounded local recoil/rumble tests are
  available. No firmware flash, factory reset or calibration overwrite happens
  automatically.

[Detailed physical controls, shared signals and manufacturer references](HARDWARE-CONTROLS.md) · [Gun Studio walkthrough](gun-studio.md) · [Calibration guide](compatibility.md)

The menu shows separate P1 and P2 aim cursors, so moving one gun keeps the
other player's last position visible. Player reassignment and game transitions
clear stale positions.

### How gun profiles reach your games

The supported launch path reads the currently assigned physical guns and prepares
the game's input files before starting it. Common actions follow the saved Studio
bindings; additional title functions use their reviewed control routes. The active
profile supplies the startup controls reference.

| Integration | How player controls are applied |
| --- | --- |
| **MAME** | Separate gun mice and raw keyboards for P1/P2. Identical Start/Coin key codes from mixed families keep their own player channels. Supported action routes include the Vyper's separate A/B actions. |
| **TeknoParrot RawInput** | Writes assigned device paths and supported mouse/key actions into the selected profile. Named title actions such as weapon change and pedals follow their reviewed routes. Inactive players and removed actions lose their old input routes. Vyper weapon change uses learned D-pad Down when its selector is reserved for recoil. |
| **Dolphin / Wii** | Uses a matching disc-region accuracy profile and the gun's captured controls. Independent bridge channels carry each player's aim and buttons, including supported mixed pairs and fully captured Sinden controls. |
| **Standalone RPCS3** | Prepares supported Raw Mouse / PS Move profiles with separate physical inputs and additional button channels. |
| **RetroArch and Supermodel** | Generates supported device/action routes from the current assignment. A captured magazine reload takes priority over a retained pedal alias. The controls check flags shared RetroArch keyboard codes rather than treating equal keys as isolated player inputs. |
| **Recognized multiplayer plugins** | Prepares each installed integration's required device format. Blue Estate and HOTD 2: Remake fixed mouse layouts temporarily switch the Vyper magazine channel to right mouse, then restore its saved mode. |

The bulk controls repair uses the same Dolphin model support and bridge policy
as game launch, including Vyper, XGunner and supported mixed pairs. The report
checks the written files and identifies remaining actions; it does not mark a
game as physically tested.

Fixed mouse layouts are not arbitrary remapping systems. The controls reference
shows their required bindings, and patches must match the installed game build.
DemulShooter and Hook of the Reaper can run as configured session helpers for
compatible games; game-driven recoil needs a matching output integration.

Independent P2 input depends on the game, emulator and installed integration. The
status distinguishes configuration, launch checks and confirmed gameplay. See the
[emulator table](../README.md#emulator-support) and [game-specific guide](compatibility.md)
for supported paths and remaining requirements.


[Back to Deadeye Arcade](../README.md)
