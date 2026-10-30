Keep active game windows and required companion processes correctly configured.

- Track the active renderer of owned packaged Unreal games when their original window survives. Dialogs, overlays and windows from other processes cannot replace the tracked renderer.
- Refresh dual-process loader metadata from the matching installed TeknoParrot vendor profile. Repair an unavailable companion path only from the exact vendor filename beside an existing main game binary, preserving working custom paths and gun bindings.
- Validate ASCII aliases for both required ElfLoader processes before writing their paths. Relative paths use the game's working directory; missing companions remain explicit setup errors.

Includes regression coverage for surviving native game windows, process identity, custom companion paths, missing files, relative paths and invalid vendor filenames. Native startup, profile routing and physical gameplay remain separate verification steps.
