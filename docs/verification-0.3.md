# Verification 0.3

The Windows application was installed and exercised in a test environment.
This report describes application behavior and validation boundaries.

## Windows checks

- WPF/WebView2 starts in fullscreen. Local covers, logos and preview videos render;
  preview playback can be paused and resumed.
- Local media hosts were verified after updating the Content Security Policy.
- Dolphin/Dead Space Extraction and MAME/CarnEvil reached their title or boot screens.
  This verifies launch paths, not physical gun gameplay. MAME's emulated GUN OK status
  is not confirmation of connected hardware.
- F12 ended a launched session and returned to the fullscreen frontend. The keyboard
  hook is active only during an app-owned game session and does not record other keys.
- Blue Estate reached its main menu after installation of the required Microsoft
  Visual C++ runtimes. The app's dependency workflow downloaded an official installer,
  verified its Microsoft signature and started it; a subsequent scan observed the
  installed runtime. Unresolved dependencies remain visible as warnings.
- The native exit button remained reachable while scrolling, selecting import files,
  searching for applications and learning buttons.
- Autostart settings and the Windows user Run entry were checked. Autostart is
  optional and defaults to off.

The hook follows Microsoft's [LowLevelKeyboardProc documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).

## Software checks

- Production UI build and self-contained Windows x64 publish passed.
- Core checks cover imports, missing files, alternative launch paths, MAME archive
  requirements and preservation of library entries, priorities and media references.
- Dependency counterexamples cover architecture mismatch, required versus delay
  imports, rescanning, .NET configuration and malformed executable files.
- Media access rejects executables, unrelated paths and symbolic links.
- Browser checks cover player isolation, aiming UI, file selection, on-screen keyboard,
  filters and four views at five widths.
- A real MP4 fixture verifies decoding, controlled pause/resume and suspension during
  dialogs, game sessions and view changes. Controlled input is not physical gun evidence.

## Remaining validation

Physical button input, aiming, calibration, separate players and recoil/rumble require
local hardware tests. Additional emulator launch paths and configured feedback helpers
need title-specific tests. File availability and imported entries are not proof that a
game starts successfully or plays correctly.
