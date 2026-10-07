Deadeye Arcade 0.3.9 improves independent multiplayer device configuration.

- Assign installed DemulShooter helpers from the actual connected P1/P2 guns before every launch.
- Isolate unassigned helper channels with nonempty identifiers so nameless Remote Desktop mice cannot control missing players.
- Reject duplicate player slots or two player assignments pointing at the same mouse.
- Merge helper settings with backups while retaining unrelated options.
- Disable conflicting native Model 2 RawInput and crosshairs when using its DemulShooter launch path.
- Show separate requirements for HOTD Remake, HOTD 2 Remake, Operation Wolf Returns, classic Windows HOTD games and Model 2.
- Assign Blue Estate VID/PID values only for an already installed patch; reject duplicate VID/PID identities. The patch must be compatible with the 32-bit game build and requires Raw Mode and fullscreen.

Third-party games, patches and game media are not bundled. Installed files, successful launch and physical two-gun gameplay are separate checks. English remains the first-run default, German is available, and autostart remains off by default.
