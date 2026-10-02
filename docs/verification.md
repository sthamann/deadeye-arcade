# Verifikation – Version 0.1.0

Datum: 2. Oktober 2026. Entwicklungsrechner: macOS / Apple Silicon. Zielpaket: Windows x64.

## Geprüft

- React-/TypeScript-Produktionsbuild erfolgreich.
- Native WPF-/WebView2-Anwendung erfolgreich für Windows x64 veröffentlicht, mit eigener .NET-Laufzeit.
- 21 Kernprüfungen erfolgreich: TeknoParrot-Lichtgun-Auswahl und Ausschluss eines Rennspielprofils; defektes XML; fehlende Spieldatei; Basename-Startparameter; Blockierung von Vorschau- und unvollständigen Einträgen; konsekutive MAME-Katalogeinträge; ROM-/Lightgun-Abgleich; RawInput-Startparameter; Wiederimport ohne Duplikate; Erhalt von Favorit und Cover; Zurücksetzen des Spielurteils bei geänderten Startparametern; Kaltladen der Bibliothek; Sicherung der vorigen Fassung; expliziter Fehler bei beschädigter Bibliothek; korrekte XML-Gerätekennung; gehaltene Ausstiegskombination und Abbruch beim Loslassen; exakter Coverabruf und Ablehnung mehrdeutiger Cover.
- Browserbedienung erfolgreich: leere Bibliothek ohne erfundene Installationen; ausdrücklich gewählte Beispielbibliothek; Spieleauswahl; Favoriten-/Plattformfilter und Suche; fünf Zielschritte; Browsergrenze beim Dateizugriff; Gun-Navigation über den Nachrichteneingang; Ablehnung eines Zieltreffers des falschen Spielers; Zuordnungsbefehl für den richtigen Spieler.
- Vier Hauptansichten bei 1440, 1024, 768, 390 und 320 Pixel Breite geprüft, ohne horizontalen Seitenüberlauf.

Der native UI-Nachrichtenvertrag wurde für diese Browserprüfung mit kontrollierten Eingaben gespeist. Das ist ein Softwaretest, keine angeschlossene Lightgun und keine Messung eines Windows-Eingabepfads.

## Noch unbestätigt

- Start der kompilierten Anwendung auf Windows einschließlich tatsächlicher WebView2-Initialisierung.
- RS3-Produktnamen, RawInput-Gerätekennungen, absolutes Koordinatenformat und Maus-/Tastaturmodus auf diesem Ziel-PC.
- COM-ID-Antworten und Modus-/Bildformatbefehle an der tatsächlich vorhandenen Hardware/Firmware.
- Spielertrennung im echten Emulator; MAME-Abgleich der Controller-Kennungen.
- Erfolgreicher Start und korrektes Ende der konkreten TeknoParrot-/MAME-/PC-Spiele.
- Rückkehr nach Abziehen/Anschließen, Neustart und möglichem USB-Portwechsel.

„Von dir bestätigt“ ist eine bewusste Nutzerangabe nach dem Spieltest, kein automatisch berechnetes Kompatibilitätsurteil.

Nächster entscheidender Schritt: [windows-test.md](windows-test.md).
