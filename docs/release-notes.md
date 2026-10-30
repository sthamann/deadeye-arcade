Replay physical button presses against your active game profiles.

- Add `--simulate-controls` to inspect the entire library without launching games. Each game test also records its own simulation result.
- Exercise down, repeated down, release and player isolation. Report the smallest physical button combination that reaches each readable action.
- Replay Dolphin DSU profiles through the production input bridge and decode actual output frames, including negated actions and button chords.
- Detect global keyboard collisions, missing endpoints, wrong player routes and unsupported expressions. Incomplete action contracts remain pending.
- Route generic coin labels by the vendor’s actual Coin1/Coin2 identifiers. Do not let a stale controller display name suppress a missing RawInput pedal binding. Existing explicit pedal routes are preserved.
- Bound each game’s simulation and keep launch, simulation and physical gameplay verification separate.

Simulation does not certify physical tracking, recoil or in-game reactions.
