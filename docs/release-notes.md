Deadeye Arcade 0.3.22 integrates a reviewed repair catalog into startup, import and game launch.

The app recognizes moved game paths, broken portable emulator paths, missing matching OpenAL runtimes, missing support files for the exact Hypseus release, access requirements and shared PCSX2 pointers. It restores eligible missing support files from hash-checked official packages and keeps existing files and upstream licenses.

Settings now shows Automatic repairs: results, remaining steps, known pitfalls and recent changes. Existing supported gun/profile adapters record their actual changed files with before/after hashes. Catalog rules and fixes are distributed with normal releases and self-updates; reports remain local.

Display diagnostics separate Remote Desktop modes from physical-session modes and record changes through the frontend and optional desktop-gun companion. No physical refresh rate or completed gun test is inferred from a remote session.

Validation includes 247 core regression checks, optional checks against both official repair archives, English/German repair UI checks and a native Windows build. Existing libraries and startup preferences are retained. Vendor access, Windows consent and physical aiming/independent P2 still need their applicable checks.
