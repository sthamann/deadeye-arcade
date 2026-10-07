# Physical lightgun controls

Gun Studio uses separate, original vector drawings for each hardware family. Numbered controls identify physical inputs; live highlights use received input tokens, independently of the assigned gameplay action. **Capture** associates a control with the signal from your own device. **Bind** assigns a gameplay/menu action. Captures and bindings persist across updates.

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

The 0.3.4 migration corrects only the exact earlier factory map. Custom action bindings remain untouched.

## Sinden

Reference: [official Sinden product details and photos](https://www.sindenshop.com/products/sinden-lightgun).

The drawing includes the trigger, pump, four side buttons and four directions of the D-pad. The vendor advertises ten inputs. Buttons may emit different inputs on-screen and off-screen; capture the intended on-screen mapping. The Sinden software controls camera tracking, border, on/offscreen bindings and start/stop. Reaper does not substitute RS3 keyboard codes for these controls.

## X-Gunner Wireless

Reference: [official product photos](https://hwhxg.com/product/xgunner-blue-p1/) and [download center](https://hwhxg.com/downloads/).

The drawing follows the front stick and two side buttons visible in the manufacturer's photos. Exact firmware key assignments are captured on the device. The current vendor download page does not provide a finished manual. The USB receiver being present does not prove that a wireless gun is awake; a received input is required for that status.

## Blamcon Vyper

References: [official Vyper specifications](https://blamcon.com/lightgun-lineup/blamcon-vyper/) and [assembly guide](https://blamcon.com/wp-content/uploads/2024/11/BlamconVyperManualV03.pdf).

The Vyper has an SMG body and stock, trigger, A/B foregrip buttons, pull-to-reload magazine, stick and Start/Select controls. The working fire selector controls recoil mode. Builds and firmware can differ; capture input assignments on the actual gun. The original drawings do not bundle the manufacturer's 3D models or photographs.

## Automatic emulator setup

MAME receives a controller profile built from the assigned gun's exact device path and action map. Supported TeknoParrot profiles receive RawInput aim, trigger, Start, Coin, Reload and recognized action bindings for connected players before launch. Only controls explicitly named in the selected profile are changed. Special pedals, dual triggers and unusual game-specific controls retain their existing configuration.

Model 2 receives the explicit ROM folder in its search configuration. Model 2 and Supermodel use the existing Windows short path for non-ASCII game folders when available; unsupported paths produce an actionable setup error. Renderer and unrelated input settings are preserved.

A stale TeknoParrot `GamePath` is repaired only when the library names exactly one existing game file. A required `GamePath2` is rebased only through a shared named ancestor and a unique existing second executable. Both profile executables are checked for missing runtimes before launch. Original profiles are backed up next to the XML. Ambiguous files remain blocked for review. A successful file/dependency check is not a gameplay or calibration verdict.

PCSX2 portable installations with an unavailable absolute memory-card folder receive a local storage folder. Existing accessible storage and input settings are preserved.
