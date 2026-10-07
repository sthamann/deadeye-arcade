# Installer and self-update verification

Verified on 7 October 2026 against the published [0.3.3 release](https://github.com/sthamann/deadeye-arcade/releases/tag/v0.3.3).

- The [Windows release workflow](https://github.com/sthamann/deadeye-arcade/actions/runs/37595851039) completed on GitHub, including the production UI build, 85 core checks, native publish, Microsoft bootstrapper signature verification and NSIS packaging.
- Published Setup and portable ZIP downloads matched their SHA-256 checksums and GitHub asset digests. The ZIP passed archive integrity checking and contains the executable, UI, license notices and update documentation; it contains no user library.
- Browser checks passed for English/German update labels, manual check and install commands, the automatic-check preference, visible download progress, and disabled installation during an active game or download. Existing navigation checks passed across four views at five widths.
- On Windows, a controlled development build with the new updater and an older assembly version (0.3.2) detected the real stable 0.3.3 release through GitHub. Selecting **Install update and restart** downloaded the published installer, verified it, closed the app, installed it and reopened the app automatically. Settings then showed installed version 0.3.3 and no newer release.
- Post-update comparison showed that game entries and gun bindings were preserved. The English preference remained selected, automatic update checks were enabled and Windows autostart remained off. Closing the updated app returned to the desktop; reopening restored the library.

Setup SHA-256: `fe97a587198ba841529b8b1404c2ee5ec5fa98411e8d2f51d6fdcefec93f0c75`.

The test machine already had WebView2. Installing a missing runtime, uninstalling the app, interruptions during installer extraction and physical gun operation were not exercised in this update test. The app and installer remain unsigned; the digest verifies download integrity and does not replace publisher signing.
