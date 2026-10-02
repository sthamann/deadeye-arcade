# Reaper Arcade 0.1

Eine Windows-App für ein gemeinsames Lightgun-Menü: große Vollbildoberfläche, getrennte Gun-Eingänge und eine lokale Spielebibliothek.

## Direkt auf dem Windows-PC starten

1. Das Paket **Reaper-Arcade-0.1.0-Windows-x64.zip** auf den Windows-PC kopieren und vollständig entpacken.
2. **ReaperArcade.exe** öffnen. Eine separate .NET-Installation ist nicht nötig.
3. Unter **Meine Guns** Spieler 1 zuordnen: Abzug an der ersten Gun drücken, dann ihre Start-Taste. Bei Spieler 1 muss Start die Taste `1` senden; bei Spieler 2 die Taste `2`. Dazu die Hardware-Spielerzuordnung und den Maus-/Tastaturmodus verwenden.
4. Den Zieltest durchführen. Er prüft fünf Ziele; er schreibt keine Kalibrierung in die Firmware.
5. Unter **Spiele finden** TeknoParrot, MAME oder eine Windows-Spielanwendung auswählen.
6. Das erste Spiel starten, Zielen und Tasten im Spiel prüfen und anschließend Start + Münze etwa zwei Sekunden halten. Auf Spieler 1 entspricht das `1` + `5`, auf Spieler 2 `2` + `6`.
7. Unter **Einstellungen** bei Bedarf Vollbild und Start nach Windows-Anmeldung aktivieren.

Vorausgesetzt werden Windows x64 und die Microsoft Edge WebView2 Runtime. Falls die Runtime fehlt, erklärt die App das beim Start. Download: https://developer.microsoft.com/microsoft-edge/webview2/ . Das Paket ist ein noch nicht signierter Entwicklungsstand.

## Was in 0.1 implementiert ist

- Native WPF-Anwendung mit lokal verpackter React-Oberfläche. Kein Webserver oder Browserfenster für den Windows-Betrieb nötig.
- Vollbild, Favoriten, Suche, Plattformfilter, Spielauswahl und Spieldetails.
- Enumeration der Windows-Raw-Input-Geräte; Erkennung der Retro-Shooter-Familie, wenn das Gerät einen passenden Produktnamen liefert. Modell und Firmware werden nicht geraten.
- Bewusste Zuordnung des Maus- und Tasteneingangs pro Spieler; kein automatisches Zusammenwerfen der Geräte.
- Getrennte absolute Zielkoordinaten und Abzugseingaben; Navigation mit Stick im Maus-/Tastaturmodus.
- Geführter Zieltest: nur die dem gewählten Spieler zugeordnete Gun wird im nativen Betrieb gewertet. Der Test zeigt Trefferabweichung und Fehlschüsse; er misst keine Latenz.
- Optionaler COM-Port pro Gun. Vor jeder Steuerung wird `ID` abgefragt und mit dem Spieler verglichen. Mausmodus und 4:3/16:9 werden in einem begrenzten Befehlsablauf angefordert; anschließend endet der externe Steuerungsmodus.
- TeknoParrot-Import bereits angelegter `UserProfiles` mit kuratierter Zuordnung bekannter Lightgun-Titel. Nicht zugeordnete Profile werden im Importbericht genannt. Ein vorhandenes Basisprofil und eine vorhandene Spielanwendung sind Voraussetzung.
- MAME-Import anhand des tatsächlichen `-listxml`-Katalogs dieses Emulators und vorhandener ZIP-/7z-ROMs. Beim Start entsteht eine eigene Controller-Datei mit den gespeicherten Gun-IDs. Vorhandene MAME-Konfigurationen werden nicht überschrieben.
- Manuelles Hinzufügen von Windows-Spielanwendungen.
- Lokale Cover sowie SteamGridDB-Abruf bei genau einer passenden Titelzuordnung. Mit gespeichertem Schlüssel folgt der Coverabruf auch nach dem Import. Der Schlüssel wird mit Windows DPAPI benutzergebunden verschlüsselt.
- Lokale Bibliothek in `%LOCALAPPDATA%\ReaperArcade`, Sicherung der vorigen JSON-Version, Wiederherstellung nach Neustart und Diagnoseexport ohne API-Schlüssel.
- Spielstart mit gespeicherten Argumenten, Beobachtung des gestarteten Prozesses und seiner Folgestarts, Rückkehr ins Menü nach Ende, gezieltes Beenden der eigenen Spielsitzung.

## Was noch folgt

- Automatische Konfiguration der TeknoParrot-Controller: 0.1 startet das bereits vorhandene Profil. Dessen Eingabebelegung muss zunächst im Emulator stimmen.
- Einbindung der Herstellerkalibrierung. Der jetzige Zieltest ist eine Funktionsprüfung der Eingabe.
- DemulShooter und Hook of the Reaper für titelabhängige Mehrspieler- und Spielefeedback-Profile. 0.1 verändert deren Konfiguration noch nicht und erzeugt keine spieleabhängigen Recoil-/LED-Ausgaben.
- DuckStation, PCSX2, Dolphin, Flycast, Model 2, Supermodel, weitere Konsolen sowie automatischer Steam-Import.
- Signierter Installer, Updates und eine zugängliche Bildschirmtastatur für seltene Texteingaben.

Der normale Spielbetrieb ist auf Guns ausgelegt. Der erste Dateiimport verwendet aktuell Windows-Auswahldialoge. Für API-Schlüssel und andere Texteingaben kann vorerst eine Tastatur erforderlich sein.

## Verifikation dieses Stands

Die Oberfläche und der Windows-x64-Build wurden auf macOS erstellt. 21 Kernprüfungen decken echte Dateifixtures, Importgegenfälle, fehlende Spielpfade, Wiederimport, Kaltladen, Covermehrdeutigkeit, MAME-Geräteabbildung und die gehaltene Ausstiegskombination ab. Die Browserprüfung testet Auswahl, Suche, Filter, Vorschaugrenze, Zieltest, Raw-Input-Nachrichtenvertrag und Spielertrennung sowie das Layout in verschiedenen Breiten.

**Das ersetzt keinen Windows-Hardwaretest.** WPF/WebView2 zur Laufzeit, echte RS3-Eingänge, COM-Kommunikation, MAME-Gerätekennungen, TeknoParrot-Prozessketten und die Rückkehr aus einem echten Spiel sind auf dem Ziel-PC noch zu prüfen. Die App startet deshalb mit einer leeren Bibliothek und vergibt keine automatisch erfundenen „spielbar“-Urteile. „Von dir bestätigt“ setzt der Nutzer nach einem tatsächlichen Spieltest.

## Weiterentwickeln

Node.js und .NET SDK 10 werden zum Bauen benötigt:

```text
cd ui
npm ci
npm run build
cd ..
dotnet run --project src/Reaper.Checks -c Release
dotnet publish src/Reaper.Windows -c Release -r win-x64 --self-contained true -o release/windows-x64
```

Die portable Windows-App benötigt den vollständigen Inhalt von `release/windows-x64` einschließlich `web/`. `scripts/build-windows.ps1` baut und verpackt diesen Stand auf Windows. `npm run dev` im Ordner `ui` öffnet eine reine Bedienvorschau, ohne Hardware- oder Dateioperationen zu simulieren.

Für die Browserprüfung einmal `npx playwright install chromium` im Ordner `ui` ausführen. Danach dort die Vorschau mit `npm run dev -- --port 5199` starten und aus dem Projektordner `node scripts/check-ui.cjs` aufrufen. Alternativ setzt `REAPER_PREVIEW_URL` die Adresse der laufenden Vorschau.

Technische Entscheidungen und Herstellerreferenzen: [docs/architecture.md](docs/architecture.md). Erster echter Test: [docs/windows-test.md](docs/windows-test.md).
