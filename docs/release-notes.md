Recognize packaged Unreal development builds as the running game.

- Follow the owned game process when a packaged development build keeps the bootstrap executable name under Binaries/Win32 or Binaries/Win64.
- Keep the configured game directory boundary, exact executable name and known launcher exclusions. Updaters and unrelated processes cannot certify a successful launch.
- Preserve the existing Shipping renderer and input focus behavior.

Validated with regression checks and a native Windows startup, foreground observation and clean exit test. Physical aiming, button response and multiplayer remain separate verification steps.
