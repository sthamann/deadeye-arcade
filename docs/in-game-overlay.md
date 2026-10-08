# Game menu and startup controls

When a recognized game window appears, Deadeye shows the available game-specific
button legend for ten seconds. The compact native window shows each assigned gun
and highlights physical controls whose active profile mapping can be resolved.
Scroll for longer lists. Unknown or unsupported mappings remain visibly unresolved.

Hold **Start + Coin for about two seconds** to end the current Deadeye game session.
This emergency path runs independently of the web interface. Release both buttons
before using the combination again. In the frontend, the same combination closes
the app. **F12** is the keyboard alternative. A ten-second trigger hold can open the
game menu when the gun continues reporting a held trigger; some games or device
modes interfere with that signal, so use Start + Coin for recovery.

**F10** opens the game menu. Its three actions are **Resume**, **Restart** and
**End game**. Aim and shoot to select, or navigate with the assigned direction
buttons and confirm with Start. Reload or Escape returns to the game. Both guns
can navigate using their own last absolute aim position.

Available mappings are read from active TeknoParrot profiles, Dolphin title
profiles, RetroArch configuration additions, recent standalone RPCS3 Raw Mouse
and PS Move files, and the managed MAME controller file. Helpers, emulator remaps
and game-specific overrides can change the final behavior; unresolved paths do
not receive invented assignments.

Opening the menu keeps the game behind a translucent menu window. Exclusive
fullscreen renderers may require a game-specific borderless mode for reliable
composition. **It does not pause the game.** Background timers, audio or inputs
may continue. The Dolphin bridge suppresses its game buttons while the menu is open.
Restart closes the owned session and helpers before launching the same entry.
Other applications are not terminated.

The menu uses a scrollable layout and positions itself within the current
monitor's bounds. Device probing preserves the existing frontend Raw Input
registration. Session cleanup releases the launcher even if input restoration
fails. Real trigger holds, aiming and simultaneous physical buttons still require
a test at the connected monitor; Remote Desktop input is not that hardware test.
