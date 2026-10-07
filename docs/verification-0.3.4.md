# Version 0.3.4 verification

Checked on Windows on 7 October 2026, using the existing installed emulator versions. Remote Desktop provides menu/rendering evidence, not a physical gun accuracy or recoil test.

## Hardware and navigation

- Manufacturer manuals/product photographs were used to draw four distinct original model silhouettes and label physical controls; references and firmware limits are in [Hardware controls](HARDWARE-CONTROLS.md).
- RS3 keyboard defaults were corrected against the manufacturer's PC diagram. Controls on other systems require device capture when firmware assignments are unknown.
- Physical control highlighting was checked independently of changing its assigned function. Saved control capture survives reload.
- Top/Up/Down targets, cover-to-featured-game scroll reset and Launch focus were checked with a long browser library and on Windows.
- Four UI pages were checked at five screen widths, including English/German switching and player-isolated input.
- One RS3 was detected and assigned in software. Physical button input, aiming, calibration, feedback and two-player operation remain pending at the real screen.

## Actual game launches

| System | Title | Observed result | Session controls |
| --- | --- | --- | --- |
| MAME | CarnEvil | Real title/menu screen | Overlay, resume, restart and exit back to frontend |
| Dolphin | Dead Space: Extraction | Real title screen and mouse crosshair | Overlay and exit |
| Windows PC | Blue Estate | Intro and main menu with Story/Arcade/Settings | Exit back to frontend |
| RetroArch | Point Blank 3 | Real rendered animated game scene | Overlay and exit |
| PCSX2 | Time Crisis II | Real GunCon2 calibration screen after storage repair | Overlay, resume and exit; calibration deferred |
| TeknoParrot | House of the Dead: Scarlet Dawn | Real fullscreen title/attract sequence after secondary-executable repair and Microsoft VC++ 2012 installation | Overlay, resume to the actual game window and exit back to frontend |
| Flycast / Dreamcast | Confidential Mission | Real Dreamcast boot, memory-card initialization and game warning screen with crosshair; title/playthrough not confirmed | Exit back to frontend |
| Flycast / Naomi | Confidential Mission (GDS-0001) | Real animated attract sequence after replacing a directory launch argument with the existing ROM archive and retaining its CHD dependency; edge-rendering artifacts still observed | Overlay, resume and exit back to frontend; rendering follow-up pending |
| Model 2 | Virtua Cop 2 | Both single-core and multicore executables crash with Windows exception 0xc0000005 after manual ROM loading, also outside the frontend | Unresolved emulator failure |
| Supermodel | The Lost World | Real fullscreen title sequence through the frontend; rendered 3D attract sequence also verified in a direct diagnostic start | Overlay, resume and exit back to frontend |

Titles outside this table are not certified by these checks. Missing game files, special pedals, dual triggers and unusual game-specific controls retain explicit setup requirements. No physical gun playthrough was performed.

## Automatic setup and regression checks

106 core checks passed, including preservation of custom bindings, independent physical-control capture, supported TeknoParrot RawInput bindings, primary/secondary path repair, both executable dependency roots, idempotent Model 2 ROM-folder repair and PCSX2 storage repair. Native Windows publishing completed with no compiler warnings/errors.

The startup dependency check reads PE imports without executing the inspected binaries. It inspects both executables in a dual-executable TeknoParrot profile. Vendor installers retain their interactive license/UAC steps. File readiness is reported separately from an actual playability verdict.

## Packaging

Windows Setup and portable ZIP were generated and published by the [version-tag GitHub workflow](https://github.com/sthamann/reaper-arcade/actions/runs/37611571191), with SHA-256 checksums. The public Setup asset was downloaded on Windows and its SHA-256 matched both GitHub's digest and the published checksum file (`75e7b710605523d759a2924adbd72fb127f87eba2fc7ec3c45808b8921ab3f4d`). Setup exited with code 0 and registered version 0.3.4; the installed binary identifies release commit `ccc7cda`. Library entries and saved bindings remained intact, English/fullscreen preferences were preserved and autostart remained off. A fresh start of the installed release showed the connected RS3 as P1, with software setup ready and the physical aim test still pending; My Guns opened with the control diagram and saved bindings. The previous release's self-update flow was tested separately in [0.3.3 verification](verification-0.3.3.md). The package remains unsigned.
