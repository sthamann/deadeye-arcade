Deadeye Arcade 0.3.12 isolates live button testing from menu actions.

- Gun Studio starts a pure button test: connected guns highlight controls without activating menu items or the Start/Coin exit gesture.
- Hold the selected gun’s physical trigger for ten seconds, then release it, to restore menu control. A mouse can also stop the test.
- Highlight the RS3 physical trigger in joystick mode as well as mouse mode.
- Keep input learning available through explicit mapping selections. Changing the trigger’s assigned action does not remove the test escape.
- Suppress legacy context menus and key actions during the test. Gun firmware shortcuts remain active in the hardware.
- Explain RS3 calibration clearly: the verified manufacturer module sends the calibration sequence to the selected gun; the separate aim test measures accuracy and applies no correction.
- Clear button-test state when a game starts, so it cannot block the in-game overlay.

Windows build, core checks and UI regressions cover the software paths. Physical trigger holds and calibration accuracy require testing at the attached screen.

Games, third-party patches and media are not bundled. English is the first-run default and German is available.
