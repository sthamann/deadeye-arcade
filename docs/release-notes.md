This update preserves Vyper controls when ARC changes several keys at once and keeps saved profiles intact for guns which are sitting out.

- Preserve distinct A and B actions during key swaps.
- Remove obsolete magazine and selector assignments after firmware settings change.
- Keep separate P1 and P2 aim cursors in the menu.
- Accept matching keyboard device identities regardless of letter case.

Validation: 351 core checks, seven UI identity cases, a Windows build and focused tests of the rendered input path. Real aiming, recoil and cooperative gameplay still require a local hardware test.
