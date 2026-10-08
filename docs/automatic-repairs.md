# Automatic repairs

Deadeye carries reviewed setup knowledge with the application. It checks the
library at startup, after import and when **Check and repair library** is selected.
It also rechecks the selected game before dependency validation and launch.
Checks run in the background so the native exit button remains responsive.

Open **Settings → Automatic repairs** to see detected problems, repairs from the
current check, recent profile changes and the catalog of known pitfalls. Both
English and German are supported. A second check leaves healthy files unchanged.

## What happens automatically

| Problem | Detection and response |
| --- | --- |
| Moved TeknoParrot game or second launcher | Repairs only an unambiguous existing library file; keeps an original XML backup. |
| Broken portable emulator storage/search paths | Adds the explicit Model 2 ROM folder, repairs inaccessible PCSX2 memory-card storage, and refreshes matching TeknoParrot vendor metadata. Existing accessible paths remain. |
| Missing OpenAL | Reads actual PE imports, checks x86/x64 compatibility, downloads pinned OpenAL Soft 1.25.2 from its official source, restores only an absent DLL and preserves COPYING. Ambiguous mixed architectures or incompatible existing DLLs require review. |
| Missing Hypseus support assets | Requires the exact Hypseus Singe 2.11.1 executable hash. Restores only absent reviewed images and `grumble.wav`; preserves LICENSE and existing assets. A healthy install needs no download. |
| P1/P2 input assignments | Uses the connected gun identities for supported TeknoParrot, DemulShooter, MAME, RetroArch, Flycast, Supermodel, RPCS3 and Dolphin adapters. Physical USB identities survive RDP; index-based adapters require actual visible RawInput mice. |
| Supported Dolphin accuracy profiles | Matches the disc region to the reviewed accuracy package, preserves learned aiming offsets and prepares independent RS3 DSU channels. Extraction uses Hybrid Ubershaders. |
| Installed multiplayer plugins | Configures recognized HOTD 2: Remake and installed Blue Estate patch inputs according to their actual device format. Unknown plugin versions retain their own workflow. |
| Supported game presentation | Adjusts known Aliens profile fields and Model 2 presentation; generic sessions retain their own renderer. Changed settings are recorded. |
| Existing Lindbergh shared libraries | For the specific supported ElfLdr2 profiles, copies only absent, valid ELF libraries from the installed game into the loader search folder. |
| Duplicate launches / difficult exit | Launch reservation and process ownership are built into every session. Start + Coin is the independent emergency gesture; a trigger hold is optional because autofire can interrupt it. |

## Problems that require a real next step

The app identifies missing required game files, TeknoParrot vendor-access and
administrator requirements, and PCSX2 Guncon2 ports sharing the same pointer.
It keeps these visible instead of inventing a working two-player configuration.
Microsoft runtime inspection remains integrated; available installers come from
Microsoft and are checked for architecture and signature. Windows consent,
installer license prompts, vendor login and physical calibration can still require
user interaction.

Deadeye does not replace game binaries, bypass licenses, restore quarantined
executables or disable endpoint protection. A profile correction establishes a
configuration change. A real hit, successful campaign and independent P2 controls
still need a physical gameplay test.

## Display and Remote Desktop

The frontend and desktop-gun companion record current and saved Windows display
modes and distinguish a remote session from a local session. The companion keeps
observing transitions while the frontend is closed, when desktop crosshairs are
enabled. It writes only when the display state changes. The Settings view labels
RDP modes explicitly; they do not establish the physical TV's refresh rate.

`display-report.json` and `display-history.jsonl` support checking the actual local
mode after an RDP disconnect. The app does not force a guessed refresh mode or
install a graphics driver based on an RDP-only measurement.

## Structured knowledge and local records

The shipped [catalog](../src/Reaper.Core/KnownFixes.json) contains each rule's ID,
name, scope, source, automation mode and verification boundary. Implementations
are reviewed application code; the catalog cannot execute scripts or arbitrary
commands. New reviewed rules ship with normal app releases and self-updates.
Official support archives are cached locally and must match their pinned hash
and size before use. Downloads have bounded time and size limits.

Under `%LOCALAPPDATA%\ReaperArcade`:

- `fixes-report.json`: latest library inspection.
- `fixes-<game-id>.json`: latest selected-game inspection.
- `fixes-history.jsonl`: actual repaired files and launch-time profile changes;
  profile changes include before/after SHA-256 hashes.
- `repair-packages/`: verified support archives.
- `display-report.json` / `display-history.jsonl`: measured session modes.

The last 50 recorded repair/configuration events appear in Settings. Diagnostic
export includes the current repair report and recent history. Reports are local;
no library, device identities or game paths are uploaded by repair checks.
With the frontend closed, the existing app also provides:

```powershell
ReaperArcade.exe --repair-library
ReaperArcade.exe --inspect-display
```

## Verification for 0.3.22

Core checks cover real path repair with backup, repeat-run idempotence, privilege
and vendor-access detection, shared versus independent PCSX2 pointers, package
hash/size validation, preservation of existing assets, rejection of absent archive
entries, actual changed-file hashes and ambiguous OpenAL architectures. Optional
checks exercise both exact official archives on fresh temporary installations.
The browser checks cover the native repair command, findings/history, both
languages and the distinction between remote and physical display modes.
