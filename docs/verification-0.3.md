# Verifikation 0.3 – 6. Oktober 2026

Der neue Stand wurde auf macOS gebaut und mit Softwareprüfungen ausgeführt. Die Anmeldung am neuen Lightgun-PC ist hergestellt. Die aktuelle Vollbildanzeige reagiert bislang nicht auf synthetische Windows-Tastenkombinationen; Installation, Migrationsstatus und physische Hardware sind deshalb noch nicht erneut geprüft.

## Bestanden

- React/TypeScript-Produktionsbuild und native Veröffentlichung für Windows x64 mit eigener .NET-Laufzeit.
- 37 Kernprüfungen mit Importdateien und Gegenfällen; zwei zusätzliche Prüfungen am tatsächlichen Übergabedatensatz: 183 eindeutige Spiele, 25 erste Prioritäten, 133 Videoverweise. Fehlende Dateien bleiben gesperrt, alternative vollständige Startwege werden bevorzugt, gemeinsame Sammlungsstarter bleiben getrennte Titel. MAME-Datenordner benötigen auch das passende ROM-Archiv; andere Klone werden nicht pauschal vorausgesetzt.
- Medien-URLs nur für vorhandene erlaubte Formate innerhalb der Medienordner; ausführbare Dateien, fremde Pfade und symbolische Links abgewiesen.
- Bisherige Browserbedienung einschließlich Spielertrennung, Zieltest, Dateiauswahl, Tastatur und vier Ansichten bei fünf Bildschirmbreiten.
- Erweiterte Browserbedienung mit 183 Titeln: Systemfilter, Prioritätsfilter, Dateistatus und native Importaktion. Eine echte MP4-Testdatei wurde decodiert und abgespielt; Pause über kontrollierte Gun-Eingabe, Dialog, Spielsitzungsnachricht und Entfernen des Players beim Ansichtswechsel geprüft.

Der Browser-Videotest verwendet eine lokale Testadresse. Die WebView2-Medienzuordnung auf Windows ist erst nach Zielinstallation bestätigt. Kontrollierte Eingaben sind kein physischer Gun-Nachweis.

## Auf Windows noch erforderlich

- Abschluss der vorhandenen Kopier-, NAS- und Konfigurationskette; Statusdateien auf Fehler prüfen.
- Installationsskript, Sicherung, Medienquellen/-kopien und Bibliothekszusammenführung unter dem tatsächlichen Spielbenutzer.
- Reale Vorschauvideos aus der Sammlung in WebView2; passende Codecs und Medienzuordnungen.
- Externe Spiele und Launcherketten, MAME-Gerätezuordnung, F12/Gun-Ausstieg und Menürückkehr.
- Explizit konfigurierte Helfer einschließlich Start, Outputs, Portfreigabe und Beendigung. Das neue Helferfeld startet keinen Helfer ohne gespeicherte Konfiguration und behauptet keinen automatisch fertigen Hook-of-the-Reaper-Pfad.
- RS3-ID, Kalibrierung, getrennte Spieler und einzelne Rückstoß-/Rumble-Impulse am lokalen Bildschirm; danach Spielefeedback.

Autostart wurde durch diese Erweiterung nicht aktiviert. Der letzte tatsächlich auf Windows ausgeführte Reaper-Stand bleibt 0.2.0; dessen Testbericht steht in [verification.md](verification.md).
