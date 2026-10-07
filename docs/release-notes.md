Reaper Arcade now ships as a Windows Setup executable, with a portable ZIP available too.

- Per-user installation without administrator rights, desktop/Start menu shortcuts and Windows uninstall registration.
- Included .NET runtime and automatic installation of Microsoft WebView2 when missing.
- Automatic checks for newer stable GitHub releases at startup and every six hours.
- Gun-friendly Settings controls for checking, downloading and installing an update, followed by an app restart.
- SHA-256 and size checks before running the installer, library backup and preservation of settings and gun mappings.
- English and German update UI. Updates are blocked during a game; automatic checks can be switched off.
- A GitHub Actions workflow builds installers, portable ZIPs and checksums for version tags.

Core and UI checks cover version selection, prerelease/downgrade rejection, untrusted URLs, malformed hashes, verified downloads, corrupt-download preservation and cancellation. Physical aiming, calibration and recoil still require local hardware tests. Games, ROMs, emulators and manufacturer utilities are not bundled. The package is currently unsigned.
