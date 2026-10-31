Prepare Linux arcade games in real ASCII directories so a readable launcher path also works when the game resolves its asset location.

- Detect Unicode physical paths behind existing ASCII junctions.
- Prepare a physical game copy while keeping original files and gun bindings intact.
- Retain sibling assets and empty save folders for pm/game layouts.
- Reuse prepared data, restore missing files and apply changed source assets while preserving unchanged player progress.
- Move removed source assets to a recoverable archive when their prepared copies have not been modified by the player.
- Check free space, replace assets atomically and recover damaged preparation metadata from a backup.
- Keep a separate profile backup for the physical path repair and enable long path support in the Windows application manifest.

Includes the cabinet control routes and packaged Unreal runtime recognition. Launch checks and profile routing do not certify physical aiming, recoil or cooperative gameplay.
