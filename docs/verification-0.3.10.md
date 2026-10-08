# Verification: 0.3.10

The release adds background RS3 HID button observation and a desktop mouse fallback for the in-game menu. The hold recognizer is covered by regression checks for the exact ten-second boundary, repeated down events, release-to-rearm, unrelated device arrival and disconnection of the held device.

All 151 core checks pass. The self-contained win-x64 build succeeds. Dolphin profile preparation is repeatable and preserves unrelated global settings. Extraction receives only its own graphics overrides; absolute IR pointing is explicit.

On Windows, two connected RS3 devices were detected. Extraction was launched through the frontend with the managed profile. Opening the menu via F10, resuming the game and ending the game back to the library succeeded. The installed profile contains the expected absolute-input and per-game graphics values. These Remote Desktop checks do not exercise physical gun input.

Hardware recognition, a successful game launch and physical aiming are separate checks. The RS3 HID primary-button assignment and physical trigger-hold behavior still need a local test. No measured input-latency improvement or independent two-gun Dolphin gameplay is claimed.
