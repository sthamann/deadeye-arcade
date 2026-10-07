# Reaper Arcade

**Your lightguns. Your games. One Windows arcade.**

A fullscreen, gun-first launcher for your existing lightgun collection. Browse games,
set up player inputs, check required runtimes, and return from a game without
reaching for a keyboard.

[![MIT License](https://img.shields.io/badge/license-MIT-86d9b0)](LICENSE)
[![Windows x64](https://img.shields.io/badge/platform-Windows%20x64-67aaf9)](https://github.com/sthamann/reaper-arcade/releases)
[![English / Deutsch](https://img.shields.io/badge/UI-English%20%2F%20Deutsch-ff8051)](#language)

**[Download Windows Setup 0.3.3](https://github.com/sthamann/reaper-arcade/releases/download/v0.3.3/Reaper-Arcade-0.3.3-Setup-x64.exe)** · [Release notes & checksums](https://github.com/sthamann/reaper-arcade/releases/tag/v0.3.3)

> **Early access.** The Windows frontend and game-session flows have been tested.
> A connected RS3 has been detected and configured in software. Physical aiming,
> the ten-second trigger gesture and game-driven recoil still require local
> hardware tests. Reaper does not mark a game as playable just because its files exist.

## See it in action

![Animated library walkthrough](docs/images/library.gif)

*Recorded from the running UI with its optional sample library and original cover
placeholders. Sample titles are not installed games; a new real library starts empty.*

![Animated gun setup and English / German switch](docs/images/gun-studio-language.gif)

*Gun Studio and language switching in the browser preview. The recording does not
simulate a connected gun or a successful hardware test.*

## What's inside

- **A fullscreen game library:** search, favorites, platform filters, game details,
  local covers, muted preview videos, screenshots and logos.
- **Lightgun Studio:** RS3 Reaper Pro, Sinden, X-Gunner Wireless and Blamcon Vyper
  system selection; grouped USB devices, P1/P2 status, live button diagrams and
  learnable menu bindings.
- **An in-game menu:** hold the assigned trigger for **at least 10 seconds**, release
  it, then shoot **Resume game**, **Restart game** or **End game**. D-pad navigation
  and Start confirmation are available too.
- **A controls reference:** P1/P2 mappings read from MAME, TeknoParrot, Dolphin/Wii
  and RetroArch configuration files. Unknown mappings and possible game overrides
  are identified rather than guessed.
- **Runtime checks:** inspect launch programs and local DLLs on startup, after
  import and before launch. Missing supported Visual C++, DirectX and .NET runtimes
  can be downloaded from Microsoft with architecture and signature checks.
- **A dependable way out:** a native **Close app · Windows** button stays outside
  the scrolling web interface. Hold Start + Coin for about two seconds to end a
  game; release both, then hold again in the frontend to close the app. **F10** opens
  the game menu; **F12** ends the current game session.
- **English and German:** English by default; change the language in Settings.
  The choice applies to the frontend, native menu, dialogs and app messages, and
  survives restart.
- **Local storage:** a backed-up JSON library, user-bound encryption for the optional
  SteamGridDB key, and diagnostic export without API keys.

The in-game menu does **not automatically pause the game**. Background game timers,
inputs or audio may continue while the menu is open.

## Get started on Windows

1. Download and run **Reaper Arcade Setup** above. It installs for your Windows user
   without administrator rights and adds desktop and Start menu shortcuts.
2. Open Reaper Arcade. The .NET runtime is included; Setup installs Microsoft
   Edge WebView2 Runtime if it is missing (an Internet connection is then required).
3. Open **My Guns**. Connect your gun, inspect P1/P2 detection, and prepare its
   software. An RS3's COM player ID is used for automatic assignment.
4. Check the live buttons and run the five-target aim test **at your physical screen**.
   This test measures accepted hit deviation; it does not write firmware calibration.
5. Open **Find Games** to import existing TeknoParrot profiles, scan MAME ROMs against
   that emulator's catalog, add a Windows game application, or import a collection handoff.
6. Launch a game, test aiming and buttons, then use the game menu to return.
   Confirm playability in Game Details only after a real test.

Fullscreen is enabled by default. **Start with Windows is off by default** and can
be enabled separately in Settings. The installer and portable package are not code-signed.
Games, ROMs, emulators and manufacturer utilities are **not included**.

### Language

Open **Settings → Language → English / Deutsch**. Existing libraries without a
language preference also start in English. Changing language preserves games,
favorites, gun bindings and the Windows autostart setting.

The browser preview remembers its own language in local storage; the Windows app
stores the choice in `%LOCALAPPDATA%\ReaperArcade\library.json`. Imported game titles,
user notes, file paths and external manufacturer/emulator interfaces keep their
original language.

## Install and update

Setup registers Reaper Arcade in Windows Installed Apps. Uninstall removes app files
and shortcuts while keeping your library, settings and media. The optional portable
ZIP remains available under [Releases](https://github.com/sthamann/reaper-arcade/releases).

The app checks **stable GitHub releases** at startup and every six hours. When a newer
installer is available, it shows an update notice. Open **Settings → App updates →
Install update and restart**. The download is checked against GitHub's SHA-256 digest
and expected size before the installer starts. Reaper closes, updates in place and
reopens. A library backup is saved before installation; gun mappings, language and
autostart preferences are preserved. Updates cannot be installed during a game.
Failed or interrupted downloads leave the running app intact. Automatic checks can
be disabled; **Check for updates** remains available. Prereleases and older versions
are excluded from automatic updates. The update request sends no library or gun data.

New version tags run [the Windows release workflow](.github/workflows/windows-release.yml):
UI build, core checks, native publish, Microsoft bootstrapper signature check, installer,
portable ZIP and SHA-256 checksums are generated and uploaded to GitHub Releases.
[Installer and updater details](docs/updates.md).

## Support at a glance

| System | Available today | Still needs verification / configuration |
| --- | --- | --- |
| RS3 Reaper Pro | USB/COM detection, automatic player assignment, mouse mode, aspect ratio, offscreen reload, bounded recoil/rumble test pulses | Physical aiming, feedback feel and title-specific game outputs |
| Sinden | Product detection, input assignment and preparation of checked manufacturer software | Local calibration, manufacturer prompts and game profiles |
| X-Gunner Wireless | Receiver/product detection, input assignment and checked configuration software | A receiver alone does not prove how many wireless guns are active; actual input is required |
| Blamcon Vyper | Product-family detection, input assignment and Blamcon ARC entry through Steam | Exact model confirmation, calibration and physical feedback |
| TeknoParrot | Import existing UserProfiles, profile launch, read configured controls | Existing input profiles and title-specific helper/output setup |
| MAME | Import actual lightgun catalog + present ROM archives, generated controller mapping, controls reference | Game overrides, calibration and real gameplay |
| Dolphin / RetroArch | Launch paths from collection handoffs; read supported control configuration | Dedicated automatic import adapters and title/core-specific overrides |
| Other systems | Executable discovery for several emulators/tools; manual or handoff launch paths | Dedicated import/input adapters and per-title tests |

RS3 recoil and grip rumble are separate mechanisms. Reaper can send test pulses;
there is **no documented continuous RS3 force slider**. Hardware switches and the
specified power supply determine mechanical recoil. Helpers such as DemulShooter
and Hook of the Reaper can be launched with a session, but still need appropriate
per-game configuration for real output-driven feedback.

## Bring an existing collection

In **Find Games → Import collection handoff**, select `spiele.json`. Existing
favorites, covers and gun bindings are retained. **Validate library** checks launch
files and TeknoParrot GamePath independently of gameplay confirmation.

With the app closed, these commands use the intended Windows game user's library:

```powershell
ReaperArcade.exe --import-collection "C:\path\to\spiele.json"
ReaperArcade.exe --validate-library
ReaperArcade.exe --check-dependencies
ReaperArcade.exe --inspect-guns
```

Reports are stored under `%LOCALAPPDATA%\ReaperArcade`. Runtime inspection covers
known PE imports and .NET runtime configurations; dynamically loaded plugins,
special DLL search paths, drivers, older .NET Framework requirements and vendor
packages may need additional setup. Unknown DLLs are never downloaded from DLL portals.

## Verification

- **85 core checks:** imports, file readiness, preservation after reload, player
  isolation, trigger hold timing, control-profile parsing, dependency detection,
  English defaults, native translations, saved language preference and update validation.
- **Browser checks:** English ↔ German switching and reload, four pages at five
  screen widths, library search/filters, raw-input routing, live button state,
  player-isolated aim tests, picker and on-screen keyboard.
- **Windows language checks:** English default with an existing library, immediate
  switching, cold restart in both languages, localized native exit button and
  CarnEvil/MAME in-game menu. Library entries and saved gun bindings remained intact;
  autostart stayed off. [language verification](docs/verification-0.3.2.md).
- **Windows game-session checks:** CarnEvil/MAME menu open, resume, restart and exit
  back to the frontend. Earlier Windows checks reached the title/menu screens
  of Dead Space Extraction/Dolphin and Blue Estate.
- **Physical gun validation remains pending:** USB/COM detection is distinct from
  button, aim, calibration and recoil verification. Not every imported title has
  been launched or tested.

Imported entries and file availability do not establish playability. Test each
title with its configured emulator and gun before confirming it as playable.

## Build and contribute

Requirements: **Node.js** and **.NET SDK 10**.

```sh
cd ui
npm ci
npm run build
cd ..
dotnet run --project src/Reaper.Checks -c Release
dotnet publish src/Reaper.Windows -c Release -r win-x64 --self-contained true -o release/windows-x64
```

Keep the complete publish output, including `web/`. The installer build also needs
NSIS (`makensis.exe`) and Internet access for the Microsoft bootstrapper. On Windows,
[`scripts/build-windows.ps1`](scripts/build-windows.ps1) builds and packages the portable app.
The application is native WPF with a locally packaged React/WebView2 interface;
it does not require a local web server in normal Windows use.

For the browser preview and its checks:

```sh
cd ui
npx playwright install chromium
npm run dev -- --port 5199
# In a second terminal, from the repository root:
node scripts/check-ui.cjs
```

The preview demonstrates the interface without performing hardware or file operations.
UI and native code share [`localization/en.json`](localization/en.json). German source
messages are the keys; English translations are the values. Keep numbered placeholders
and surrounding spaces intact. [Localization details](docs/localization.md).

Further implementation notes: [architecture](docs/architecture.md),
[Gun Studio](docs/gun-studio.md), [in-game menu](docs/in-game-overlay.md),
[Windows checks](docs/verification-0.3.md). These earlier detailed notes are currently in German.

## License

Reaper Arcade's application code is licensed under the **[MIT License](LICENSE)**.
Third-party runtimes and components retain their own terms; see
[THIRD-PARTY.md](THIRD-PARTY.md) and the packaged `licenses/` folder.
