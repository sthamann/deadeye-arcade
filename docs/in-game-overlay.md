# Spielmenü in Reaper Arcade 0.3.1

Während einer von Reaper gestarteten Spielsitzung den gespeicherten Abzug von P1 oder P2 mindestens zehn Sekunden ohne Unterbrechung halten. Das native Vollbildmenü öffnet sich. Erst loslassen, dann eine Aktion auswählen. Kurze Schüsse werden nicht aufaddiert; Tastatur-Wiederholungen verlängern oder verkürzen die Schwelle nicht. Ein gehaltenes Signal öffnet das Menü einmal und muss vor dem nächsten Versuch losgelassen werden. Neue Spielsitzungen und getrennte Geräte setzen den Haltezustand zurück.

Die drei großen Aktionen sind **Weiter spielen**, **Neu starten** und **Spiel beenden**. Mit der Gun darauf zielen und den Abzug drücken. Die gespeicherten Steuerkreuzaktionen wählen alternativ eine Aktion; Start bestätigt, Nachladen kehrt zum Spiel zurück. F10 öffnet das Menü als Tastaturalternative, Escape schließt es und F12 beendet weiterhin die eigene Spielsitzung. Der Launcher bietet während einer Sitzung außerdem „Spielmenü öffnen“ an.

Für beide Spieler erscheinen Modell, Spielprofilbelegung und die separat gekennzeichnete Gun-/Menübelegung. Die Tastenpositionen und Bezeichnungen der Gun sind von ihren gespeicherten Eingangssignalen abhängig; die Spielprofilbelegung stammt aus folgenden Dateien:

- **MAME:** der beim Start erzeugte Reaper-Controller, einschließlich Münze, Start und Taste 1/2/3. Individuelle MAME-Spielkonfigurationen können ihn überschreiben; die Anzeige bestätigt keinen laufenden Zustand aus MAME.
- **TeknoParrot:** das gestartete UserProfile, mit den echten ButtonName-Bezeichnungen und der gewählten RawInput-/DirectInput-/XInput-Belegung. RawInput-Geräte werden gegen die gespeicherten Guns geprüft. Unbekannte Profilgeräte bleiben als offene Zuordnung sichtbar. Externe Helfer können zusätzliche Belegungen vornehmen.
- **Dolphin/Wii:** WiimoteNew.ini aus dem expliziten Benutzerordner, Portable-Modus oder Standardbenutzerordner. A, B, Plus/Minus und Steuerkreuz werden getrennt für P1/P2 gelesen. Titelprofile und Kommandozeilen-Overrides sind noch nicht aufgelöst.
- **RetroArch:** retroarch.cfg beziehungsweise explizit übergebene Konfiguration und Zusatzdateien. Gun-Aktionen, A/B, Start und Select werden angezeigt. Core-/Content-Overrides und Remaps sind noch nicht aufgelöst.

Andere Systeme erhalten keine erfundenen Spielbelegungen. Die Anzeige erklärt dann, dass nur die gespeicherte Gun-/Menübelegung vorliegt. Keine Emulator-Konfiguration wird für die Anzeige verändert.

Das Spielmenü ist ein eigenes natives Windows-Fenster. Beim Öffnen werden nur Fenster der von Reaper erkannten eigenen Spielprozesse minimiert; „Weiter spielen“ stellt sie wieder her. Das macht auch den Wechsel aus exklusivem Vollbild möglich. **Es ist keine Spielpause:** Timer, Ton und gegebenenfalls Hintergrund-Eingaben des Spiels können weiterlaufen. Ein Neustart beendet zuerst die eigene Sitzung und ihre Helfer vollständig, stellt den Menümodus wieder her und startet danach denselben Bibliothekseintrag erneut. Andere Anwendungen und die Spielekopie werden nicht beendet.

Die Zehn-Sekunden-Schwelle, vorzeitiges Loslassen, Wiederholungen, erneutes Scharfschalten und Spielertrennung sind durch Kernprüfungen abgedeckt. Der Auslöser über die echte Gun bleibt ein lokaler Hardwaretest, weil Remote Desktop ihre Eingaben auf dem getesteten PC ausblendet.

Am 7. Oktober 2026 wurde auf einem Windows-PC eine echte CarnEvil-/MAME-Sitzung aus Reaper gestartet: F10 öffnete das Menü mit der P1-RS3- und MAME-Belegung, „Weiter spielen“ stellte CarnEvil wieder her, „Neu starten“ startete denselben Eintrag erneut, und „Spiel beenden“ kehrte ins Frontend zurück. Das Windows-Aktivitätsprotokoll bestätigt die Folge `launch → opened → resume → opened → restart → launch → opened → end`. Andere Emulatoren sind damit noch nicht im Spiel getestet.

Quellen für Profilformate: [TeknoParrot JoystickMapping](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi.Common/JoystickMapping.cs), [Dolphin Wiimote](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/HW/WiimoteEmu/WiimoteEmu.cpp). Windows kann die Vordergrundaktivierung beschränken: [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow). Kein injizierter DirectX-/Vulkan-Hook wird verwendet.
