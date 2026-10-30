# Deadeye Arcade 0.3.35

Vyper detection now restores Mouse/Keyboard mode before the first game starts. This fixes a setup gap where buttons could work while the desktop pointer remained disabled in Gamepad mode.

My Guns includes **Restore mouse pointer**, which sends only the documented emulation-mode command and ends serial mode. It preserves calibration slots, IR layout, button assignments and feedback settings. Serial discovery and communication run away from the interface thread. Scroll controls sit against the viewport edge so they no longer cover the new recovery action.

The Vyper setup guide explains the other required check: with RS3 emitters at the four screen edges, choose **Crosshair** under Blamcon ARC Tracking. Check four visible points in ARC Live View, then calibrate. This physical camera check remains separate from USB detection and working buttons.

Includes the startup, cancellation, mixed receiver/player identity and learned exit gesture corrections from the current maintenance release. Validation includes 348 core checks, seven UI device identity cases and the Windows build.

Vyper firmware 2.x settings are now read directly over USB, including Start, Select, directions and magazine. Setup selects the correct mouse-mode key map and an independent magazine channel, verifies the device values, and feeds them into the existing player profiles. Native settings and bounded feedback tests use the firmware 2.x protocol. Calibration and physical player identity are preserved.
