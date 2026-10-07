Deadeye Arcade 0.3.7 improves automatic dependency setup for older game helpers.

- Detect .NET Framework 3.5 requirements directly from CLR 2 executable metadata, including configured launch helpers without runtimeconfig files.
- Respect explicit CLR 4 startup overrides and avoid treating CLR 2 DLLs inside newer hosts as standalone programs.
- Offer the built-in Microsoft feature installer on Windows through build 27999; use the official standalone Microsoft installer on Windows 11 build 28000 and later.
- Verify framework registration and architecture-specific runtime files when checking installation.
- Stop an owned signature-verification process when its two-minute timeout expires, so a stalled validation does not leave it running in the background.

129 core checks passed and the Windows build completed without warnings or errors. See docs/verification-0.3.7.md for Windows checks and remaining physical gun / P2 limitations. Autostart remains disabled by default; games and third-party tools are not bundled.
