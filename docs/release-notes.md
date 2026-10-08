Deadeye Arcade 0.3.17 restores reliable remote buttons and keeps the app exit available during button testing.

- Windows session detection follows live transitions between the physical screen and Remote Desktop. A process-start snapshot previously left remote clicks blocked after connecting to an app started locally.
- The native Close app · Windows button works during button testing, including a deliberate lightgun trigger aimed at it.
- Remote mouse clicks can end a button test; returning to the physical screen restores the gun-test input isolation.
- Includes the original eye-and-target Windows icon and matching arcade wallpaper introduced in 0.3.16. Artwork and setup notes are in docs/branding.md.

Game libraries, input settings and startup preferences are retained.
