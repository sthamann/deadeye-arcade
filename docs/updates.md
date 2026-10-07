# Windows installation and updates

The NSIS Setup executable installs per user, by default under
`%LOCALAPPDATA%\Programs\Reaper Arcade`. It includes the self-contained .NET app,
local web UI, license notices and Microsoft's Evergreen WebView2 bootstrapper.
WebView2 is installed only when its runtime registry entry is missing. The build
verifies Microsoft's Authenticode signature before packaging the bootstrapper.

Libraries, settings, covers and media stay in `%LOCALAPPDATA%\ReaperArcade`, outside
the program directory. Uninstall deletes only files shipped by the installer and
keeps user data. Setup never enables autostart; an existing app autostart entry is
updated to the installed executable. Installation refuses to overwrite a running
Reaper process. For an app-requested update, Setup waits for Reaper to close and then
restarts the installed app.

## Update path

1. The app requests GitHub's latest stable release endpoint over HTTPS, with a product
   User-Agent. No account token, library content or hardware identifiers are sent.
2. A newer numeric version must have exactly one matching Windows x64 Setup asset,
   a URL under the configured repository and a valid GitHub SHA-256 digest.
3. The download streams into a temporary file, enforces the release size, limits the
   download host and verifies SHA-256. A failed download removes its partial file and
   preserves an earlier verified installer.
4. The app saves settings and backs up its library, launches Setup with the current
   program directory and closes. Setup installs and reopens Reaper in that directory.

Automatic checks run at startup and every six hours while idle. Installing an update
requires the Settings button and is blocked during a game. Checks can be disabled;
manual checks remain available. Network errors are shown in Settings and never prevent
normal use or closing the app. Closing during a download cancels it.

The GitHub digest checks download integrity; it is not a publisher signature. The
application installer is currently unsigned. GitHub account/repository security
remains part of the update trust boundary. Windows protection settings are not changed.

## Build

Run `scripts/build-windows.ps1` with Node.js, .NET SDK 10 and NSIS installed. The script
produces a Setup executable, portable ZIP and checksum file. `-SkipInstaller` builds
only the portable package. `scripts/package-installer.ps1` can package an existing
Windows publish directory. Version tags must match the native project version.
The GitHub Actions Windows workflow executes this path and uploads the release assets.

References: [NSIS per-user execution](https://nsis.sourceforge.io/Reference/RequestExecutionLevel),
[Microsoft WebView2 deployment](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution),
[GitHub release metadata and digests](https://docs.github.com/en/rest/releases/releases).
