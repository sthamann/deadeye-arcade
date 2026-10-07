Deadeye Arcade 0.3.8 adds richer game information and safer media enrichment.

- Show short English descriptions, edition-specific release years and original hardware in the featured game area and Game Details.
- Identify demos and unreleased prototypes explicitly instead of inventing a commercial release year.
- Apply local enrichment manifests only after exact game identity and every replacement media file have been checked. Launch commands, player controls, favorites and verification status are preserved.
- Keep enriched information when re-importing a collection and store source references alongside each game.
- Audit actual local cover, preview-video, screenshot and logo files with `--audit-media`.
- Reset a failed preview when selecting a different video. Media remains private to the user's library and is not bundled with the app.

Core, UI and Windows verification are documented in docs/verification-0.3.8.md. Autostart remains disabled by default.
