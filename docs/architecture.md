# Aufbau und erste Grenzen

`Reaper.Core` enthält Dateiformate, Import, Persistenz, MAME-Mapping, Coverbeschaffung und Startregeln ohne Windows-Abhängigkeit. `Reaper.Windows` besitzt das WPF-Fenster, WebView2, RawInput, bewusst ausgewählte serielle Ports und gestartete Spielsitzungen. `ui` enthält die React-Oberfläche. `Reaper.Checks` überprüft kritische Kernpfade mit Dateifixtures und kontrollierten HTTP-Antworten.

## Eingaben

Windows RawInput liefert die Gerätekennung getrennt vom gemeinsamen Windows-Mauszeiger. Absolute Gun-Pakete werden mit dem vom Gerät angegebenen primären/virtuellen Bildschirm auf die tatsächliche WebView-Fläche umgerechnet. Relative Mauspakete verwenden den Windows-Cursor als Einrichtungshilfe. Ein synthetischer UI-Klick folgt dem Abzugspaket; legacy Mausklicks im WebView werden unterdrückt, damit ein Schuss nicht zweimal auswählt. Tastennavigation verwendet im Mausmodus die normalen Tastenereignisse der Gun.

Die Zuordnung wird am konkreten Maus- und Tasteneingang aufgenommen. Ein Produktname kann die Retro-Shooter-Familie erkennen, ist aber kein Firmwarebeleg. MAME-Stabilität hängt von der Gleichheit der RawInput-Gerätekennungen auf diesem Ziel-PC ab. USB-Portwechsel und fehlende eindeutige Hardware-Seriennummern können eine Neuzuordnung erfordern.

## Serieller Weg

Die App öffnet ausschließlich einen explizit ausgewählten COM-Port. `ID` muss mit `id=1` bis `id=4` antworten; die Antwort muss der vorgesehenen Spielerposition entsprechen. Konfigurationsbefehle werden mit mindestens 120 ms Abstand gesendet. Für Modus/Bildformat lautet die Folge `ZS`, `ZM`, `ZW` oder `ZN`, `ZX`. Bei einem Fehler nach Eintritt in den externen Modus wird `ZX` bestmöglich nachgesendet. Es gibt in diesem Stand keinen automatischen Recoil-Dauertest und keinen parallelen Portbesitz mit einem Feedback-Helfer.

## Spiele und Prozesse

TeknoParrot wird über den Basename seines Profils gestartet, nicht über einen frei zusammengestellten Shell-Befehl. MAME liefert den Katalog selbst. Es werden nur ROM-Archive für Lightgun-Maschinen eingelesen; ROM-Vollständigkeit ist erst im Emulator bekannt. Die Argumente gehen einzeln an `ProcessStartInfo.ArgumentList`.

Die Sitzung merkt PID und Startzeit eigener Prozesse. Neue Nachfahren und innerhalb der Startphase neu auftauchende Prozesse mit dem exakt eingestellten Spielpfad werden verfolgt. Vor einem Beenden wird die Startzeit erneut verglichen; frühere Prozesse oder PID-Wiederverwendung werden nicht übernommen. Zunächst wird ein normales Fensterende angefordert, nach 1,8 Sekunden werden verbleibende verfolgte Prozesse dieser Sitzung beendet. Sehr kurze Launcherketten, externe Dienste, Spiele ohne sichtbaren neuen Prozess und privilegierte Prozesse benötigen den Ziel-PC-Test und gegebenenfalls einen spezifischen Adapter.

## Oberfläche und Speicher

Nur `https://reaper.local/` darf native UI-Befehle senden. Navigation zu anderen Ursprüngen und zusätzliche Browserfenster werden blockiert. Die Oberfläche liegt im Paket; API-Abfragen erfolgen im nativen Kern. Cover werden als lokale Bilder unter einem separaten virtuellen Host gelesen. Eine Browservorschau besitzt diese Fähigkeiten nicht und kennzeichnet sich ausdrücklich.

Bibliothek und Covers liegen in `%LOCALAPPDATA%\ReaperArcade`. Vor dem Ersetzen einer vorhandenen Bibliothek bleibt eine `.bak`-Datei bestehen. Beschädigte Bibliotheken werden nicht stillschweigend geleert. SteamGridDB-Schlüssel werden mit Windows DPAPI verschlüsselt; Diagnose und UI-Zustand erhalten den Klartext nicht.

## Primärreferenzen

- [RS3-Handbuch 2026](https://retroshooter.com/wp-content/uploads/2026/02/Retro-Shooter-Reaper-User-Manual-2026.pdf): Gerätefunktionen, ID und COM-Befehle.
- [Microsoft RawInput](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input): getrennte Geräteeingänge.
- [WebView2 für WPF](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf): nativer Host.
- [MAME Stable Controller IDs](https://docs.mamedev.org/advanced/devicemap.html): `mapdevice` und seine Grenzen.
- [MAME Kommandozeile](https://docs.mamedev.org/commandline/commandline-all.html): RawInput und Lightgun-Eingang.
- [TeknoParrotUI Startparameter](https://github.com/teknogods/TeknoParrotUI/blob/master/TeknoParrotUi/App.xaml.cs): `--profile` und UserProfiles.
- [SteamGridDB API v2](https://www.steamgriddb.com/api/v2): Metadaten und Cover.
- [Hook of the Reaper / RS3](https://hotr.6bolt.com/pmwiki.php/Tutorial/RS3Reaper): nächster Feedback-Adapter, einschließlich dokumentierter Geräteeigenheiten.

Der erste Hardwaretest hat Vorrang vor einer Erweiterung der Emulatorliste. Danach: Herstellerkalibrierung, TeknoParrot-Inputprofile, DemulShooter/Hook of the Reaper und weitere Emulatoradapter.
