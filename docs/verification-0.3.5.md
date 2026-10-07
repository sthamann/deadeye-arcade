# Deadeye Arcade 0.3.5 verification

Checked on 7 October 2026.

- Renamed the existing public GitHub repository to `sthamann/deadeye-arcade`; Git history and existing releases were retained.
- Built the production UI and Windows x64 app. All 106 core checks passed, including strict update-address and SHA-256 validation for the new repository and installer name.
- Browser checks passed for language persistence, navigation, search/filters, player isolation, binding messages, gun controls and four pages at five widths.
- Captured the running frontend with an external curated selection of media. The recordings verify that videos decode and advance, that selection changes the featured title, that selecting a cover returns to the top, and that pause/resume affects the video player. The screenshot preview uses game imagery; Gun Studio does not simulate attached hardware.
- Kept executable, data folder, registry and mutex IDs unchanged for existing installations. The installer uses the existing registered directory, migrates the app-owned shortcuts to the new display name and preserves autostart preferences.

Versions 0.3.4 and earlier need a one-time manual Setup update because they reject download URLs under the renamed repository. The new client uses the new endpoint; no download-address or checksum checks were relaxed.

The physical-gun and unresolved emulator limits in [0.3.4 verification](verification-0.3.4.md) still apply. This release changes branding and documentation; it does not establish additional game or hardware compatibility.

## Published installer and Windows upgrade

The [Windows release workflow](https://github.com/sthamann/deadeye-arcade/actions/runs/37615562327) completed successfully and published Setup, portable ZIP and checksums. The public Setup was downloaded on Windows and its SHA-256 matched GitHub's asset digest and the checksum file: `9f19c5d9b75d20c14800a049626a9b0e8b5df4ddc7843bec05c7da6afa9be035`.

Setup exited with code 0. The installed binary reports product `Deadeye Arcade`, version `0.3.5+3a079d2`; the existing installer registration reports 0.3.5. Before/after comparison confirmed unchanged library entries and gun bindings, English/fullscreen preferences and disabled autostart. The new Deadeye desktop shortcut exists.

The public GitHub README was opened in a browser: every new screenshot and animated GIF loaded successfully. The old repository URL redirects to the new repository. The full-resolution UI MP4 uses H.264 at 1680 × 1050.
