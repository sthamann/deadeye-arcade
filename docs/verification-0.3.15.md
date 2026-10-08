# Deadeye Arcade 0.3.15 verification

## Automated checks

- 174 core checks pass. New cases cover the exact two-second chord threshold, repeats, partial release, rearming only after both buttons release, trigger pulses versus a held emergency chord, independent physical player routing, duplicate-device rejection, screen edges and negative virtual-desktop origins.
- Windows x64 self-contained build: zero errors and warnings. Production UI build and existing browser checks pass.

## Windows execution

- Settings toggles the separate desktop-gun companion. Disabling stops it and persists across frontend restart. Enabling starts it, writes its own Windows startup entry, and closing the frontend leaves the companion alive.
- The companion detects two assigned RS3 mouse devices separately. Remote Desktop movement does not manufacture player positions. Physical marker movement remains a local test.
- Scarlet Dawn was launched through the frontend and reached a foreground game window. A controlled synthetic P1 Start + Coin hold of 2.3 seconds caused the independent exit handler to end the game and restore the frontend. The activity log records the independent exit, and no Scarlet Dawn / TeknoParrot session processes remained.
- This is a software-input game-session test, not a physical gun or machine-gun-stage test.

## Behavior boundaries

The emergency watcher has its own message thread and a keyboard-state fallback; termination is not queued behind the frontend UI. It still requires the frontend process and Windows input services to be alive and runs on the ordinary interactive desktop. Other gun models need learned keyboard Start/Coin controls for this fallback; the existing Raw Input gesture remains available.

Desktop markers only display fresh absolute packets from a uniquely assigned device. They hide on other applications and unplug events. They are passive, click-through markers; Windows keeps one shared system mouse pointer.
