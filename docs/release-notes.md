Deadeye Arcade 0.3.21 preserves exact lightgun assignments when Remote Desktop hides their RawInput mouse interfaces.

A healthy, present USB interface can retain its assigned device-name profile. Mouse-index profiles are refreshed only when the actual RawInput mice are visible, so a remote launch no longer silently replaces valid gun assignments with disconnected placeholders or guessed mouse indices. Keyboard-only visibility no longer counts as live aiming input.

Time Crisis 5 now shows the credit, weapon, cursor and pedal bindings from the recognized game-scoped AutoHotkey helper. Its trigger is shown only when the assigned DemulShooter mouse matches. Modified helpers, duplicate helpers and another cabinet side do not inherit this legend. Two players still require linked cabinets for this title.

This release includes regression checks for remote USB retention, genuinely disconnected devices, local mouse indexing and the Time Crisis 5 legend. Existing libraries, startup preferences and the recognized HOTD 2: Remake multiplayer plugin setup are retained. Physical aiming, button actions and recoil require verification with the actual guns at the display.
