Recognize packaged Unreal games that receive input through a hidden window.

- Keep the visible renderer available for presentation and overlays while recognizing a hidden top-level UnrealWindow of the exact same game process as input focus.
- Limit the exception to packaged Shipping game targets, a visible non-minimized renderer and a non-minimized hidden Unreal input window. Standard Windows dialogs, child controls and foreign processes remain excluded.
- Add native Windows regression cases for the real input-window relationship and the rejected alternatives.

Retains automatic companion executable repair, cumulative library tests, per-gun profiles and legacy graphics repair. Native startup, profile routing and physical gameplay remain separate verification steps.
