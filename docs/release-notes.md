Deadeye Arcade 0.3.15 adds a dedicated emergency exit and independent desktop gun markers.

- Hold the same player's Start + Coin buttons together for two seconds to end the current game. Release both before using the gesture again.
- A dedicated input thread monitors a keyboard hook and keyboard state independently of the frontend UI. Termination runs outside the UI dispatcher. Trigger autofire does not reset this gesture.
- Show blue P1/P2 crosshairs for assigned absolute-input guns on the Windows desktop and Explorer. Each marker uses its own physical device coordinates; Remote Desktop's shared pointer is not impersonated as a gun.
- Keep markers available with the frontend closed, through a separate background companion and startup entry. Settings can disable them. Games and other applications hide the markers, and marker windows never intercept clicks.
- Stop and restart the companion during installer upgrades, and remove its startup entry on uninstall.
- Add timing, partial-release, autofire, player-routing and desktop-coordinate regressions. Core checks: 174 passing.

The trigger-hold menu remains optional. A total Windows freeze, secure desktop or terminated frontend cannot be covered by a software-only emergency guarantee. Physical gun buttons and desktop movement need local validation; Windows retains one shared system click pointer.
