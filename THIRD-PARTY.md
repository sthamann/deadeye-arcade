# Third-party notices

Deadeye Arcade's application code is licensed under the [MIT License](LICENSE).
Dependencies retain their own licenses. Games, ROMs, emulators and manufacturer
utilities are not bundled with Deadeye Arcade.

| Component | License / source |
| --- | --- |
| React and React DOM | [MIT](https://github.com/facebook/react) |
| Lucide icons | [ISC](https://github.com/lucide-icons/lucide) |
| .NET Runtime and WPF | MIT and additional component notices; distributed runtime licenses are included under `licenses/`. [Runtime](https://github.com/dotnet/runtime), [WPF](https://github.com/dotnet/wpf) |
| Microsoft WebView2 SDK | [Microsoft license](https://www.nuget.org/packages/Microsoft.Web.WebView2). The separate WebView2 Runtime is distributed by Microsoft. |
| System.IO.Ports / ProtectedData | [MIT, .NET Runtime](https://github.com/dotnet/runtime) |
| NSIS installer runtime | zlib/libpng license and compression component licenses; included in `licenses/nsis-COPYING.txt`. Unmodified upstream source: [NSIS download](https://nsis.sourceforge.io/Download). |
| Vite / TypeScript / Playwright | Development tools; not run as Windows processes in the portable app. |

The generic cover placeholders are original application assets. README recordings
show the running interface with a curated selection of game artwork and preview
clips. Third-party game artwork remains subject to its owners' rights. The
application license does not grant rights to games, ROMs or artwork.

## Separately downloaded setup assets

- [Dolphin Lightguns Accuracy INIs](https://github.com/ProfgLX/Dolphin-Lightguns-Accuracy-Inis)
  are GPL-3.0 assets. The app downloads a verified revision into the user's data
  directory and preserves its LICENSE alongside generated, game-specific profiles.
  These profiles and their upstream assets are not bundled as MIT application code.
- The RS3 calibration DLL is obtained separately from the manufacturer's public
  calibration package. Its exact package and DLL hashes are checked before use.
  It retains the manufacturer's terms and is not redistributed in the application
  source or release packages.

- [OpenAL Soft](https://openal-soft.org/) 1.25.2 is downloaded separately, only when
  a matching game lacks OpenAL. The app checks the official package hash, copies
  the matching implementation DLL and keeps the upstream COPYING file. OpenAL
  Soft retains its LGPL terms; it is not bundled as MIT application code.
- [Hypseus Singe](https://github.com/DirtBagXon/hypseus-singe/releases/tag/v2.11.1)
  2.11.1 support images and a sound file can be restored separately for the exact
  recognized installed executable. Its upstream LICENSE is retained. No emulator
  or game data is included in the Deadeye installer.
