# Library validation and control checks


The built-in library check launches games one at a time through the same setup
and input path used by the frontend. Loading time is separate from observation:
a game gets a full 15 seconds after its visible game window appears. Each test
checks process ownership, foreground focus and cleanup, then reads the active
control profiles for the selected P1/P2 guns.

Window tracking follows a replacement renderer even when the original window
remains alive. Dolphin's running renderer is distinguished from its library and
configuration windows in both display modes, so raising its library does not
redirect the game overlay. Replacing the renderer starts a fresh observation interval.

Launch results and control completeness are recorded separately. Required
controls come from the installed MAME driver's input metadata, the selected
TeknoParrot vendor template, reviewed Wii title requirements and the supported Time
Crisis 5 helper. Other systems stay marked for review until their complete
required-action list is known. A generic button list never certifies a title's
full controls. Software checks do not mark physical aim, recoil or cooperative
play as tested.

For maintenance, close the frontend and run `ReaperArcade.exe --test-library`.
Each game report includes a button simulation alongside its launch result.
Run `ReaperArcade.exe --simulate-controls` to check the entire library without
launching games. The report in `controls-simulation.json` records the smallest
physical button combination for each readable action and separation from the
other player's controls. Dolphin DSU profiles replay button press, repeat and
release events through the production input bridge and decode its actual output
frames, including combined and negated actions. Other supported profiles use
expression simulation, labeled `profile-routed`; their consumer transition
results remain unset. Unknown syntax, missing captures, shared keyboard
collisions, conflicting special actions and incomplete required-action lists stay open.
RetroArch keeps alternative bindings for each action and reports offscreen
shooting separately from reload. Simulation never
marks a game's reaction, physical tracking or feedback as verified.

Flycast writes a separate mouse and keyboard profile for each selected gun.
Trigger, offscreen reload, the secondary action, Start, credits and directions
follow the saved gun assignments. A Vyper magazine captured as a keyboard input
is supported alongside its mouse buttons. The controls reference and simulation
read these base device profiles and check their player ports. Conflicting physical
bindings are rejected before files change. Game specific Flycast overrides and
complete per-title action requirements remain marked for review.

For imported Naomi and Atomiswave entries that point at a CHD folder, Deadeye
selects the exact same-named arcade ZIP beside that folder when it exists.
It keeps the disc folder as a requirement, validates the archive before launch
and includes the selected archive in resume checks. An unrelated neighboring
archive is never substituted. Dreamcast disc entries retain their own launch path.

Game specific TeknoParrot routes also map Gashaaaan Throw to each player's
trigger and Too Spicy's separate movement pedals to the captured left and right
controls. Exact native action identifiers keep these routes separate from other
games that share an emulation profile. For supported Aliens launch paths, the
owned Game Running status window is minimized once the game renderer is ready.
Packaged Unreal games also keep their active renderer when the game replaces its
first window. Foreground dialogs and windows from other processes cannot take its
place in Deadeye's session tracking.
Some packaged Unreal games receive input through a hidden top-level
`UnrealWindow` while continuing to draw into a visible renderer. Deadeye recognizes
this exact same-process input window as game focus, keeps the visible window for
overlays, and still rejects standard dialog windows, child controls and foreign processes.

When an installed TeknoParrot vendor profile requires a second executable, Deadeye
refreshes the corresponding loader metadata and repairs an unavailable companion
path only when the exact vendor filename exists beside the main game binary.
Working custom paths and gun bindings are preserved. Both required ElfLoader
paths receive validated physical ASCII locations when needed, with a backup of
the profile. Linux games that resolve an ASCII junction back to a Unicode folder
get a real prepared game copy. Originals are retained, `pm/game` keeps its sibling
assets, and missing prepared files are restored. Later source asset changes reach
the prepared copy while unchanged player saves remain intact. Executables and
libraries are also checked by content so binary patches are detected even when
file size and timestamps stay unchanged. Removed source
assets are kept outside the active game in a recoverable archive.

Preparation checks available disk space and requires room for the game copy on
the drive containing Public Documents. The first preparation copies the data;
later launches reuse it. Linked data inside the source folder requires a physical
source copy first. Preparation failures remain visible instead of starting a
known broken path.
Missing companion files remain visible setup errors.

The original 32 bit Windows House of the Dead 2 has a reviewed graphics repair.
For `HOD2.EXE` using DirectDraw, Deadeye downloads the exact official DDrawCompat
0.7.2 package when the wrapper is missing and disables its T&L device with
`SupportedDevices = hal`. Package and DLL hashes are verified, the upstream
license is retained, and existing display preferences are preserved with a backup.
Unknown installed wrappers remain untouched and are flagged for review. The
wrapper and its settings also invalidate stale resume results when changed.
This repairs the graphics startup path; player setup and gameplay remain separate
checks.

Use `--test-library --resume` to keep successful results whose app version,
gun assignments and relevant files still match. Resume first reaches titles
left untested by an interrupted run, then retries failures. Its cumulative attempt
list survives further interruptions, so each resume can continue reaching new titles. Missing game files
are reported with their concrete paths. An absent target rejected before a game
window exists remains a failed entry while the batch can continue with other
games. Unavailable security diagnostics for an attempted launch, recorded blocks
and malformed diagnostic results still require attention. Executables and
libraries are fingerprinted by content, including patches that preserve file
size and timestamps.
**Start + Coin** or **F12** stops the run, including between games;
`ReaperArcade.exe --stop-library-test` also stops it.

Results are saved after every title in `%LOCALAPPDATA%\ReaperArcade\library-test.json`
and the individual `game-test-*.json` files. A bounded supervisor stops stuck
workers and their owned process trees, so one failed game cannot leave the
entire run waiting indefinitely. Saved gun settings are restored after a forced
worker stop; a failed restore stops the batch for attention.

A lingering modal Windows dialog also stops the run for attention. Critical
loader errors are returned to the process, preventing orphaned error boxes from
interfering with later games. Code Integrity checks report the blocked file and
policy when Windows rejects a game or emulator component. Deadeye does not
change that security policy automatically.
Standard dialog messages are retained in the local report, including the error
body, to make missing-runtime and startup problems easier to diagnose.

Exact game action routes cover supported special inputs such as Castlevania’s
red/yellow whip buttons and Let’s Go Island 3D’s left/right trigger variants.
Mars Sortie's weapon change is routed separately for each selected gun.
Cabinet test-menu directions and explicitly labeled skip-game debug controls
are excluded from required gameplay actions; real selection controls remain included.
The reviewed Wii profiles are checked across all sixteen model pairings in
software. Physical button presses and in-game reactions still require hardware
verification.

MAME checks include global and per-game overrides, preserving their unrelated
settings and backing up changed input files. Time Crisis 5's reviewed helper is
adapted to the selected gun for credits, both pedals, weapon change and crosshair.
Custom helpers are retained for review. TeknoParrot checks compare the active
profile with the independent template shipped by the emulator, keep managed pedal
routes aligned with the selected guns while preserving explicit external pedals, detect missing
rows and stale action identifiers, and restore supported routes with backups.
Aim routes are checked against the exact physical gun for each player. Unknown
action ownership stays pending. Each game report records the app version and
the exact tested gun assignments. Per-configuration results are retained under
`%LOCALAPPDATA%\ReaperArcade\test-history`, so testing another gun pair does
not erase previous evidence. Changing profiles, helpers or assignments
invalidates a retained result. Equal keyboard codes shared by two RetroArch
guns are reported as a player isolation problem.


[Back to Deadeye Arcade](../README.md)
