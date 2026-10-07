# Verification of 0.3.8

138 meaningful core checks passed. The added checks cover real library enrichment, cold reload, provenance retention, reimport preservation, rejected game identities, atomic batch rejection, impossible years and missing replacement files. Windows compilation completed without errors or warnings.

The UI suite checks selected game descriptions, edition years, original hardware, prototype status, English/German labels and the Windows display size. A deliberately corrupt video fell back to a still image, and selecting the next game resumed real MP4 playback. The suite also retains navigation, language persistence, player isolation, settings, updates and layout checks.

## Windows checks (2026-10-07)

- The enrichment CLI updated the configured library on Windows, with presentation fields and actual nonempty local cover, preview-video, screenshot and logo files confirmed by its media audit.
- The Windows frontend rendered an English game description, edition year and original hardware in both the featured area and Game Details. The preview video advanced between observations, and the native Close app button returned to Windows.
- Newly added media were decoded or image-verified locally before transfer. Preview clips were encoded as H.264 MP4 for WebView2. Source-title checks found incorrectly named original assets; replacements use matching title and platform references.

This is media and frontend verification. It does not certify aiming, recoil, independent P2 input or every game launch. Earlier game-specific results and open issues are recorded in [0.3.7 verification](verification-0.3.7.md) and the [compatibility guide](compatibility.md). Personal media, collection manifests and source-storage paths are not part of the public release.
