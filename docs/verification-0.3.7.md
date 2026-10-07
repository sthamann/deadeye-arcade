# Verification of 0.3.7

129 core checks passed, including a metadata-only CLR 2 helper, installed/missing framework results, an explicit CLR 4 startup override, a CLR 4 control and the official installer catalog. Windows compilation completed without warnings or errors.

The older-Windows feature route and newer-Windows standalone route follow [Microsoft installation guidance](https://learn.microsoft.com/en-us/dotnet/framework/install/dotnet-35-windows-11). The build 28000+ route has not been tested on a target machine.

## Windows checks (2026-10-07)

- The 0.3.7 candidate scanner checked the installed library on Windows. It detected the actual x64 DemulShooter helper and an x86 CLR 2 title, and correctly reported their installed framework. No recognized runtime findings remained missing. Unresolved imports and unreadable files still require separate review.
- The public 0.3.7 GitHub installer was downloaded, its SHA-256 matched the release digest, and Windows installation registration reported version 0.3.7. A fresh dependency scan through the installed release reported no missing recognized runtime findings. English default, fullscreen, automatic update checks and disabled autostart were retained. Microsoft Visual C++ 2013 x64 and .NET Framework 3.5 installation dialogs both reported success.
- Time Crisis 5 rendered its attract sequence. A keyboard coin remapping reached its mode and stage selection. Its standalone screen explicitly reported that two-player cooperative play was unavailable. This is not a linked-cabinet or physical gun verification.
- After framework installation, Time Crisis 5 again accepted the keyboard coin input and reached mode/stage selection. Ending the game returned to the library and the owned game, DemulShooter and AutoHotkey processes were no longer running. Physical gun input and helper injection remain unverified.
- The House of the Dead 2 Remake reached its multi-lightgun assignment screen. The plugin loaded and initialized; P1 requires a physical trigger assignment and remains unassigned. Ending the game returned to the library.
- RS3 calibration preparation reached Calibration ready. Physical screen calibration remains disabled over Remote Desktop and still needs an on-site aim test.

The [0.3.6 verification notes](verification-0.3.6.md) cover Dolphin launch, active title mappings, menu/return behavior and the broader UI checks. Physical aim, trigger holds, recoil, Dead Space Extraction tutorial actions and independent P2 remain open. The existing Model 2 crash and Flycast rendering issue are not resolved by this dependency update.
