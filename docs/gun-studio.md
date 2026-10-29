# Lightgun Studio

Open **My Guns** to choose a gun for each player, identify physical buttons, adjust actions and prepare manufacturer software. Windows groups a gun's mouse, keyboard, HID and serial interfaces by its USB Container ID. An ordinary mouse or a generic vendor ID alone does not establish a supported lightgun. A wireless receiver and a verified active gun are separate states.

The header shows connected devices and the assigned P1/P2 guns. Select **Use for P1/P2** to assign a device; moving an already active gun swaps the slots. **Sit out** frees its slot and retains its saved profile. A hardware player number remains independent of its game slot.

The Studio and native startup overlay share original model drawings. The numbered list identifies the actual controls; opposite-side controls are labelled separately. Physical signal capture and action assignment are independent. Unidentified inputs are listed explicitly. The menu wizard captures Start, Coin/Select and directions in sequence, requires release and rejects duplicate signals. For Sinden it also captures the pump and all four side buttons, covering its nine non-trigger controls. The button test displays inputs without menu actions; firmware shortcuts on the gun itself remain active.

Actions show physical button names. Supported MAME, TeknoParrot RawInput, Dolphin, RPCS3, RetroArch and Supermodel routes use common action assignments at launch. Additional title functions retain their individual controls. Flycast and game-specific multiplayer extensions prepare their supported device and button routes, while fixed mouse layouts keep their required bindings. The game controls show the resulting profile. Restoring defaults preserves learned physical signals. Emergency exit uses physical Start and Coin/Select independently of action remaps.

Connected gun families trigger software preparation. RS3 uses Windows HID/serial and the checked calibration module. Sinden and XGunner packages come from reviewed official downloads; their program files are scanned for supported Microsoft runtimes, missing required components open an official installer and setup scans again afterwards. Cancelling an installer leaves a visible manual action instead of repeatedly opening it. Sinden's new configuration enables automatic detection/start and its screen border while preserving vendor license/recoil confirmations. Blamcon ARC installs through Steam; a pending login or install confirmation remains visible. The Blamcon gun performs tracking onboard and does not require ARC to remain running.

RS3 calibration runs for the selected gun at the physical display. XGunner calibration uses its onboard workflow. Vyper supports saved calibration profile selection and documented native configuration; ARC provides full camera, smoothing and calibration controls. No automatic firmware flash takes place. Received commands, received buttons and successful physical aiming are distinct checks.

See [physical controls and manufacturer references](HARDWARE-CONTROLS.md) for button layouts, shared signals, calibration and feedback details.

## Set up a physical gun

1. Connect the device and inspect its card in **My Guns**. Choose its P1 or P2 game slot. Leave other devices in standby with **Sit out**.
2. Select the matching model and player in the Studio. Open **Setup & checks** and complete any visible manufacturer software or runtime action.
3. Open **Buttons & live input**. Use guided menu setup for unknown controls, then capture any extra physical inputs individually. Press and release each requested button; duplicate menu signals are rejected.
4. Use **Start button test** to check the diagram without issuing normal menu actions from that gun. Select an action row to change a binding. Restore defaults if needed; learned physical signals are retained.
5. Calibrate through the model's supported workflow at the physical display, then open the firing range.
6. Launch a supported game and compare its startup controls reference with the gun. Check both players separately and together before marking gameplay tested.

## Default action layout

These are Deadeye's action choices. Capture retains the actual signal configured in the gun or manufacturer software.

| Family | Shoot | Reload | Additional actions | Start, Coin and navigation |
| --- | --- | --- | --- | --- |
| RS3 Reaper Pro | Trigger | Shared grip reload input | Magazine base; title-specific side/stick actions | Hardware P1/P2 key codes, with captured overrides |
| Sinden | Trigger | Captured pump | Captured rear-right and front-right side buttons | Front-left for Start, rear-left for Coin and captured D-pad |
| XGunner | Trigger | Right mouse or Space | Middle mouse and Q/E are second-action aliases by default; title profiles can use distinct named controls | Start/Enter, Coin and WASD stick |
| Blamcon Vyper | Trigger | Magazine pull through FOOT channel | A is the second action; B is the third action; captured selector input remains available | Captured Start, Select and stick directions |

A shared electrical signal cannot distinguish two physical controls. This applies to the Reaper grip buttons and to a Vyper magazine/pedal on the same FOOT channel. The XGunner's mechanical magazine release has no separate HID signal documented in the reviewed manual. Hardware power, mode and feedback switches are shown as hardware controls rather than bindable button inputs.

## Firing range

**My Guns → Firing range** opens an input test with a small shooting game. It uses the assigned connected guns and keeps their device identities separate.

Each player has a colored crosshair, twelve rounds, shots, hits, reloads, Coin count and a displayed weapon selection. Two hits destroy a barrel; it returns for another round. Reload replenishes only that player's magazine. Second, third and mode actions cycle the displayed weapon. Held inputs highlight the model drawing, while the readout identifies the latest physical button and assigned action. The control history also shows received buttons with no action binding.

Start and Coin remain test inputs here and do not navigate away. Aim and trigger at **Back to My Guns** to return. The native **Close app · Windows** button remains available. Changing the assigned devices rebuilds the range's player state, and input recovery clears held-button state.

The range consumes button events directly, avoiding the loss of quick press/release pairs through UI state batching. Mouse aiming in browser preview is a demonstration; actual input isolation and aim testing use connected guns at the physical display. Range ammunition does not drive recoil from game events.

## Game profile preparation

At launch, supported profiles resolve the selected gun's saved physical signals and action bindings. MAME maps each player's raw keyboard and gun mouse separately. TeknoParrot updates recognized RawInput rows in the selected profile. Dolphin uses independent bridge channels for supported assignments and requires captured controls for Sinden. RPCS3 prepares supported Raw Mouse/PS Move paths. RetroArch and Supermodel select captured magazine reload ahead of a retained pedal alias.

Recognized Blue Estate and HOTD 2: Remake plugins use fixed mouse bindings. For these paths, the Vyper FOOT channel temporarily emits right mouse, the legend shows that effective route and the saved menu mode returns after the session. This does not make arbitrary Studio remaps work with a fixed-layout plugin.

The game reference shows the generated or active mappings, including unresolved actions. Game support still depends on the installed build, disc profile or matching plugin. [Game-specific requirements](compatibility.md).

## Software preparation and feedback

| Family | What Deadeye prepares | What remains in the manufacturer workflow |
| --- | --- | --- |
| RS3 | Windows interface detection, COM settings and checked calibration module | Physical calibration and hardware recoil switches/power |
| Sinden | Reviewed package, supported runtime scan, fresh configuration and vendor app launch | Vendor confirmations, camera/border controls, calibration and supported recoil patterns |
| XGunner | Reviewed configuration package, supported runtime scan, mode/aspect setup and local feedback pulses | Changed key maps, IR arrangement, onboard calibration and physical switches |
| Vyper | Official ARC Steam installation action, native settings, saved calibration slot and local feedback pulses | Steam login/confirmation, ARC camera/smoothing/rumble controls and onboard calibration |

Progress, failures and pending actions remain beside the gun. Runtime installers are checked again before the vendor app starts; cancelling an installer leaves a visible action. Vyper and XGunner local feedback tests send a bounded pulse and restore the automatic feedback mode. Game-event recoil needs a matching output integration; a successful serial write is not a hardware acknowledgment.
