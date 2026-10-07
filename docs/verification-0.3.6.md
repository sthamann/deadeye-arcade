# Verification of the 0.3.6 source candidate

This records the source candidate, not an installed or physically verified release.

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

## Outstanding acceptance checks

The new candidate still needs installation and fresh game launches on Windows.
Its native manufacturer calibration transport needs a physical monitor/gun test,
followed by the separate aim test. The vendor DLL does not acknowledge firmware
acceptance; successful calls must not be reported as proven calibration accuracy.

Independent Dolphin P2 configuration remains open. Game-specific Dead Space
Extraction actions need a physical tutorial test. The House of the Dead Remake
plugin/build/multiplayer configuration needs inspection on the target computer.
Blue Estate's separate two-gun patch is not automatically installed. The previously
recorded Model 2 crash and Flycast rendering issue are not fixed by this change.

See [compatibility and setup](compatibility.md) for the supported paths and sources.
