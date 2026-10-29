# Physical lightgun controls

Gun Studio uses original vector drawings for each hardware family, shared by the Studio and native game overlay. Numbered controls identify physical inputs; live highlights use received input tokens, independently of the assigned gameplay action. **Capture** associates a control with the signal from your own device. **Bind** assigns a gameplay/menu action. Captures and bindings persist across updates.

## RS3 Reaper Pro

Reference: [Retro Shooter's PC button diagram, page 7](https://retroshooter.com/wp-content/uploads/2026/02/Retro-Shooter-Reaper-User-Manual-2026.pdf).

| Control | P1 factory input | P2 factory input |
| --- | --- | --- |
| Trigger | Mouse left | Mouse left on P2's mouse |
| Grip reload buttons | Mouse right | Mouse right on P2's mouse |
| Magazine base | Mouse middle | Mouse middle on P2's mouse |
| Start / Coin | 1 / 5 | 2 / 6 |
| Stick up / down / left / right | Arrow keys | U / V / W / X |
| Stick press | Q | S |
| Side button | M | N |

Holding Coin for four seconds sends Escape. The recoil selector and DIP switches are physical settings. Both grip buttons share the same signal; an offscreen shot may also send a reload signal. HID alone cannot distinguish controls that emit the same token. Changed firmware or vendor mappings should be captured on the device.

Factory migrations preserve custom action bindings. Restoring default actions also preserves captured physical signals.

## Sinden

Reference: [official Sinden product details and photos](https://www.sindenshop.com/products/sinden-lightgun).

The drawing includes the trigger, pump, four side buttons and four directions of the D-pad. The vendor advertises ten inputs. Buttons may emit different inputs on-screen and off-screen; capture the intended on-screen mapping. The Sinden software controls camera tracking, border, on/offscreen bindings and start/stop. Reaper does not substitute RS3 keyboard codes for these controls.

## X-Gunner Wireless

Reference: [official product photos](https://hwhxg.com/product/xgunner-blue-p1/) and [download center](https://hwhxg.com/downloads/).

The USB receiver can expose generic Windows mouse, keyboard and serial names. Detection uses the XGUNNER receiver serial together with VID `1209` and PID `0001` through `0004`, grouped by physical Windows Container ID. Receiver presence and a received wireless input remain separate states.

The [X-GUNNER manual](https://ae01.alicdn.com/kf/Sa61a9e8ad5144b66a36d572d7548dc22q.pdf) supplies the physical controls: trigger/left mouse, right and middle mouse buttons, Start, `5` (Coin), Space, Q, E, the Enter button on the opposite side and WASD. The diagram also identifies physical power, vibration and rapid-fire switches, the magazine release and the firmware button. Enter is labelled separately from the directional joystick. Capture modified firmware mappings on the actual device. The magazine release is documented as a mechanical release for the battery magazine. The manual does not document a separate HID signal for it; Deadeye therefore does not invent a programmable reload input for that part. Use the documented right mouse or Space input for reload actions.

Calibration is performed by the gun: disable Rapid Fire, press Space + Start, pull the trigger, then confirm center, up, down, left and right. Changing square/diamond IR layout requires recalibration. Deadeye opens the checked V261009 official configuration tool for calibration, button settings and firmware information; firmware is not flashed automatically.

Serial configuration is restricted to a currently identified XGunner receiver: `G` lightgun mode, `V` 16:9, `Q` 4:3, `E` automatic feedback. Local bounded tests use `S6` then one `F0` solenoid or `F1` motor pulse and restore `E` even after failure. Sending a command is not an acknowledgment of calibration or physical feedback. Game event feedback requires an appropriate game output integration.

Game P1/P2 selection does not change the receiver’s hardware number. Every gun retains its own mapping while unselected, and RS3 serial commands and calibration continue to address their actual hardware IDs.

## Blamcon Vyper

References: [official Vyper specifications](https://blamcon.com/lightgun-lineup/blamcon-vyper/) and [assembly guide](https://blamcon.com/wp-content/uploads/2024/11/BlamconVyperManualV03.pdf).

The Vyper has trigger, A/B foregrip buttons, magazine reload, joystick directions, Start/Select and a fire selector. Trigger/A/B default to left/right/middle mouse. Keyboard defaults are not guessed: capture Start, Select and the directions once. Capture also updates Deadeye menu actions. Start + Select supplies the emergency chord after both keyboard controls have been captured. The wiring diagram connects the magazine pull microswitch to the FOOT channel. In assignable pedal mode its default is keyboard R, used as reload in Deadeye. A is the second action and B is the third action. A connected pedal shares that channel, so HID cannot distinguish it from the magazine pull. ARC key remaps must be captured on the actual gun. The two-stage mechanical trigger uses one trigger microswitch; Deadeye does not advertise two independently reported trigger stages.

The native configuration uses the [documented Blamcon protocol](https://blamcon.com/get-started-with-blamcon/serial-commands/), 9600/N/8/1 and `+` termination. `CM.0.0` requests mouse/keyboard mode; reviewed `CM.1`, `CM.2`, `CM.3`, `CM.8` and `CM.12` values configure reload, pedal, aspect, recoil and existing calibration slots. No firmware flash, factory reset, player-ID rewrite or automatic calibration takes place. Per-game aspect settings restore the saved menu settings afterwards. Bounded `FB.0.1` and `FB.1.1` pulses finish with deactivation and `ES`, including failure cleanup. This establishes command delivery, not hardware acknowledgment or game-event feedback.

The [ARC manual](https://blamcon.com/support/manual/advanced-remote-console/) covers full tracking, rumble PWM, firmware and calibration. Both guns initially use hardware P1; change one to P2 in ARC, then select game slots independently in Deadeye. Calibration captures center, top, bottom, left and right with a green status LED and saves onboard. The in-gun shortcut documented in [troubleshooting](https://blamcon.com/get-started-with-blamcon/troubleshooting/) is B held off screen for four seconds, followed by two trigger presses. Keep ARC's selected calibration profile in sync with Deadeye. Smoothing and IR sensitivity remain visible manufacturer settings.

Dolphin profiles use the magazine pull for reload, A for the Wii A action, B for the third action, and A+B for shake. Learned Start/Select and directions retain per-device isolation, including two Vypers or a mixed pair. Missing controls stop automatic Dolphin setup with a capture instruction rather than silently substituting Reaper keys. The startup legend resolves the resulting physical Vyper controls.

## Test and game routing

My Guns includes a two-player firing range with destructible barrels, independent ammunition, magazine reload, weapon changes and live physical button highlights. It consumes the actual assigned devices, without merging them into the desktop mouse. Unassigned buttons remain visible as received signals. Start and Coin do not leave this test; aim at Back to My Guns or use the persistent native Exit button.

Supported generated profiles use the same physical/action definitions. MAME receives separate raw keyboard indices as well as separate gun mice, including mixed families that both emit 1/5. Captured magazine reload takes priority over a retained pedal alias in RetroArch and Supermodel. Fully captured Sinden controls can use Dolphin's per-player bridge. Installed Blue Estate and HOTD 2: Remake plugins require a fixed right mouse reload route, so Vyper's FOOT mode changes temporarily for those games and restores its saved menu setting afterwards.

These routes are checked through generated emulator configurations and separate player input frames. Physical tracking, wireless delivery, feedback and each game's complete two-player behavior still need testing at the connected display. Unsupported title actions and uncaptured vendor controls are not certified by a successful USB scan.
