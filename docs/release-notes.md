# Deadeye Arcade 0.3.34

This maintenance release improves startup, mixed lightgun setups and returning safely to Windows.

* Closing the app during game preparation cancels the pending launch. A game no longer starts after its frontend was closed.
* A failed WebView2 initialization saves the complete error in activity.log and closes the frontend cleanly so it can be reopened.
* Recovering USB input also releases the button test state, preventing Start, Coin and direction buttons from remaining blocked after a session change.
* RS3 button diagrams, the menu guide and the firing range use the receiver's hardware player identity even when that gun is assigned to a different game slot.
* The menu exit gesture uses each gun's physical Start and Coin buttons, including captured Vyper controls. Hold both for two seconds, then release both before using the gesture again.
* Starting a game runs the automatic repair pass once. Repeated launch requests are ignored while the same game is starting or running.
* Restart cleanup waits briefly for owned helper processes to exit before accepting another session.

The release checks include 340 core regression checks and seven UI device identity cases. Actual aiming, feedback and multiplayer button verification still require the relevant guns and games at the physical screen.
