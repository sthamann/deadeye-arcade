# Verification of 0.3.6

Software checks and Windows smoke tests are recorded separately from physical gun gameplay.

- 124 core checks passed, including calibration trigger/release sequencing,
  cancellation, ISO/WBFS disc IDs, title-profile installation and preserved
  Dolphin settings, learned RS3 physical controls, per-game overlay mappings,
  independent Supermodel player devices and clearing unavailable P2 inputs.
- Windows native build and self-contained x64 publish succeeded without warnings
  or errors. Cross-compilation alone does not prove native hardware behavior.
- TypeScript/Vite production build succeeded.
- Browser checks passed for English/German, gun-only menu navigation, player
  isolation, live physical buttons, all page layouts at five widths, calibration
  package and selected-player bridge commands, and Remote Desktop calibration
  restrictions.

## Windows smoke tests (2026-10-07)

- The Windows package was installed with a backup of the previous application
  and library. Native library, gun-discovery and dependency checks completed.
- An RS3 P1 was detected with a healthy Windows driver and responded to its
  serial identity probe. This does not establish physical input or aiming accuracy.
- Dead Space Extraction launched through Deadeye into Dolphin and rendered its
  startup and animated game scenes. The disc ID selected the RS3 title profile,
  which appeared in the in-game menu. Ending the session returned to the library.
- A preparation indicator that remained visible after Dolphin returned was
  corrected, rebuilt and retested on Windows. The library and its video preview
  resumed with the indicator cleared.
- Time Crisis 5 launched through Deadeye and rendered its attract sequence.
  Its in-game menu opened, and ending the session returned to the library.
  Its game-specific input mapping and linked-player operation remain unverified.
- Selecting a library card scrolled back to the game's launch area. The native
  Close app control returned to the Windows desktop.
- Windows autostart remained disabled.

These checks used Remote Desktop with mouse/keyboard navigation. They do not
demonstrate physical trigger holds, gun-only operation, recoil or independent P2.

## Outstanding acceptance checks

Its native manufacturer calibration transport needs a physical monitor/gun test,
followed by the separate aim test. The vendor DLL does not acknowledge firmware
acceptance; successful calls must not be reported as proven calibration accuracy.

Independent Dolphin P2 configuration remains open. Game-specific Dead Space
Extraction actions need a physical tutorial test. The House of the Dead Remake
plugin/build/multiplayer configuration needs inspection on the target computer.
Blue Estate's separate two-gun patch is not automatically installed. The previously
recorded Model 2 crash and Flycast rendering issue are not fixed by this change.

See [compatibility and setup](compatibility.md) for the supported paths and sources.
