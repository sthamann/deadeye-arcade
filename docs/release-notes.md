Deadeye Arcade 0.3.10 improves the in-game escape menu and Dolphin setup.

- Observe RS3 joystick button edges as well as mouse and keyboard input.
- Keep the ten-second trigger hold intact when unrelated devices arrive or change.
- Cancel a hold when its actual input device disconnects.
- Add an independent desktop mouse hold fallback during games launched by Deadeye.
- Log bound trigger press/release events for diagnosing a physical gun test.
- Use explicit absolute pointing in managed Dolphin RS3 profiles.
- Apply a Dead Space: Extraction-only graphics preset: VSync off, 3x internal resolution, no MSAA, asynchronous ubershaders and pre-launch shader compilation.
- Preserve existing game settings and make repeated profile preparation idempotent.
- Include the scrollable first-launch game information and always-accessible launch button.

151 core checks pass and the Windows build succeeds. Physical aiming latency and the RS3 joystick trigger mapping require a test at the attached display. Dolphin independent two-gun gameplay remains unverified; this release does not enable it.

Games, third-party patches and media are not bundled. English remains the first-run default, German is available, and autostart remains off by default.
