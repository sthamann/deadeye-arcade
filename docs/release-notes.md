Continue library tests past an explicitly missing game target.

- Report the actual missing file before a generic setup status can obscure it.
- Keep an absent target rejected before a game window exists as a failed entry, while allowing unrelated games to be tested. This also handles stale paths on an unmapped drive.
- Still stop for unavailable security diagnostics on attempted launches, recorded blocks and malformed diagnostic metadata. No game is marked passing by this exception.

Retains automatic legacy House of the Dead 2 graphics repair, the Aliens launcher handoff, matching Flycast arcade archives, additional per-game control routes and cumulative resume progress. Native launch, profile routing and physical gameplay remain separate verification steps.
