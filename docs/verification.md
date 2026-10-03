# Verifikation – Version 0.2.0

Datum: 3. Oktober 2026. Build auf macOS / Apple Silicon; Laufzeittest per Remote Desktop auf Windows-PC **5090STH**.

## Auf Windows beobachtet

- Native WPF-/WebView2-App erfolgreich installiert und mehrfach neu gestartet; Cinema-Layout im Vollbild sichtbar.
- Navigation, automatische Installationssuche, große Dateiauswahl (geöffnet und abgebrochen) und Bildschirmtastatur ausgeführt.
- Desktop- und Startmenü-Verknüpfungen vorhanden.
- Autostart in der Oberfläche aktiviert; nach Neustart der App `startWithWindows=true` und passender HKCU-Run-Eintrag nachgewiesen. Kein Windows-Neustart in dieser Sitzung.
- Tatsächlicher Sitzungsweg mit einem temporären Windows-Profil geprüft: `cmd.exe /c "timeout /t 10"` gestartet, App minimiert, externes Programm sichtbar, nach Ende Vollbildmenü wiederhergestellt. Aktivitätslog enthält den Start. Testprofil anschließend entfernt; Bibliothek wieder leer.
- Die begrenzte Suche in üblichen Verzeichnissen meldete keine unterstützte Emulatorinstallation. Dies sagt nichts über andere, manuell auszuwählende Ordner aus.

Die RS3-Guns waren laut Nutzer nicht angeschlossen. Remote Desktop bildet zudem nicht den physischen Gun-Eingabepfad ab.

## Automatische Softwareprüfungen

- React-/TypeScript-Produktionsbuild bestanden.
- Native Windows-x64-Veröffentlichung mit eigener .NET-Laufzeit bestanden.
- 26 Kernprüfungen bestanden: echte Importdateien/Gegenfälle, TeknoParrot-Lightgun-Auswahl, defektes XML, fehlende Spieldateien, Startregeln, MAME-Katalog/ROM-Abgleich, Geräteabbildung, Wiederimport, Favoriten/Cover-Erhalt, Rücksetzen geänderter/fehlender Startprofile, Kaltladen und JSON-Sicherung, beschädigte Bibliothek, Covermehrdeutigkeit, gehaltene Ausstiegskombination, Installationssuche und gefilterte Dateiauswahl.
- Browserbedienung bestanden: leere echte Bibliothek, ausdrückliche Vorschau, Auswahl/Filter/Suche, Zieltest, Raw-Input-Nachrichten und Spielertrennung, Rückkehrschaltfläche nach Zieltest, Ordnerauswahl mit Gun-Nachricht, Bildschirmtastatur, Remote-Mausweg, korrekte Beschriftung der Bibliotheksprüfung.
- Vier Hauptansichten bei 1440, 1024, 768, 390 und 320 Pixel Breite ohne horizontalen Seitenüberlauf geprüft.

Kontrollierte Raw-Input-Nachrichten sind Softwaretests und keine Hardwaremessung.

## Noch offen

Echte RS3-Geräteerkennung, RawInput-Koordinaten und Tasten, COM-ID/Modus/Bildformat, Herstellerkalibrierung, USB-Neuanmeldung, Windows-Neustart sowie Zielen/Mehrspieler/Beenden in den tatsächlichen Emulatoren. Die externe Kalibrier-EXE ist auswählbar/startbar implementiert, auf diesem PC ohne Guns nicht ausgeführt.

TeknoParrot-/MAME-Import ist mit Dateifixtures geprüft; ein echter Titel war in dieser Windows-Sitzung nicht verfügbar. Automatische TeknoParrot-Controllerbelegung, DemulShooter-/Hook-of-the-Reaper-Feedback und weitere Emulatoradapter sind noch nicht fertig.

„Von dir bestätigt“ bleibt eine bewusste Nutzerangabe nach dem Spieltest. Nächste reale Runde: [windows-test.md](windows-test.md).
