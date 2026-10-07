# Verifikation 0.3 – 7. Oktober 2026

Version 0.3 ist auf dem Windows-Lightgun-PC unter dem Spielbenutzer installiert und ausgeführt. 183 Titel sind importiert, alle 639 Medien aus dem Übergabeplan wurden kopiert und anhand ihrer Dateigröße geprüft. Die Spiele-/Emulatorkopie und abschließende Konfiguration laufen noch. RS3-Guns sind nicht angeschlossen.

## Tatsächlich auf Windows geprüft

- Installer abgeschlossen, bestehende App/Bibliothek gesichert, Desktop- und Startmenü-Verknüpfungen angelegt. Die Medienkopie umfasst 4.406.888.342 Bytes.
- WPF/WebView2 startet im Vollbild. Eigene Covers, Logos und ein echtes Vorschauvideo der Sammlung sind sichtbar; Pause und Fortsetzen funktionieren.
- Der erste Videostart scheiterte an der Content Security Policy. Die lokalen Medienhosts sind jetzt ausdrücklich für Bilder und Videos erlaubt; die native Medienzuordnung wurde danach auf Windows bestätigt.
- Dead Space Extraction wurde durch Reaper mit dem gespeicherten Dolphin-Startweg gestartet. Wii-Sicherheitssequenz und echter Spieltitelbildschirm wurden angezeigt. Das ist ein Boot-Nachweis, kein Gun-Spieltest.
- Der erste F12-Versuch per Remote Desktop scheiterte, weil diese Sitzung keine passenden Raw-Input-Tasteneingaben lieferte. Der ergänzte Windows-Tastaturhook ist nur während einer eigenen Spielsitzung aktiv, verarbeitet nur F12 und wartet nicht im Callback. Nach der Reparatur wurde derselbe Spielstart erneut ausgeführt: F12 wurde im Aktivitätslog um 07:20:38 erfasst; Dolphin endete und Reaper kehrte sofort ins Vollbildmenü zurück, vor dem Zeitlimit des zusätzlichen Testabbruchjobs.
- CarnEvil wurde über den echten Reaper-MAME-Startweg gebootet: `CARNEVIL V1.0.3`, `CMOS OK`, `GUN OK`, `DCS2` waren um 07:38 sichtbar. `GUN OK` ist die emulierte Arcade-Platine, keine Bestätigung einer angeschlossenen RS3. F12 beendete die Sitzung mit Rückkehr zu Reaper. Ein vorher störendes Windows-Terminalfenster wurde durch fensterlosen MAME-Prozessstart entfernt.
- Blue Estate scheiterte um 07:40 an `MSVCP100.dll`/`MSVCR100.dll`. Visual C++ 2010 x86 wurde anschließend aus der offiziellen Microsoft-Quelle mit gültiger Microsoft-Signatur und ausdrücklicher Lizenzfreigabe installiert; beide DLLs waren um 07:49 in `SysWOW64` mit Version `10.00.40219.325` vorhanden. Nach den folgenden Laufzeitinstallationen erreichte derselbe Reaper-Startweg um 08:24 das echte Blue-Estate-Hauptmenü (32-bit, DX9; Story, Arcade, Settings). F12 beendete das Spiel und brachte Reaper zurück ins Vollbild. Gun-Spieltest offen.
- Die neue Abhängigkeitsprüfung wurde aus der tatsächlich installierten App per CLI und in der Oberfläche ausgeführt. Der Download, die Microsoft-Signaturprüfung und der Start des VC2013-Installers funktionierten über den neuen App-Befehl. Nach Nutzerabschluss waren um 08:18 VC2013 x86 (`12.00.40664.0`) und VC2010 x64 (`10.00.40219.325`) vorhanden. Nachprüfung um 08:20: 183 Spiele, 455 unterschiedliche Programme/lokale Bibliotheken, keine erkannten fehlenden Katalogpakete; 97 Hinweise auf nicht aufgelöste oder noch nicht vollständig prüfbare Dateien bleiben sichtbar. Das bestätigt keinen vollständigen Abhängigkeits- oder Spielbarkeitstest.
- Der native Knopf „App schließen · Windows“ blieb beim Scrollen, bei geöffneter Importdateiauswahl und während der laufenden Installationssuche sichtbar. Klicks beendeten Reaper unmittelbar mit sichtbarem Windows-Desktop; nach dem Dateiauswahltest wurden keine laufenden Reaper-Prozesse gefunden. Der Knopf liegt außerhalb des WebView2-Fensters und wird nicht durch Browserdialoge oder Ladezustände gesperrt.
- Start + Münze beendet im Spiel die eigene Sitzung und im Menü Reaper. Eine ausgelöste Kombination bleibt gesperrt, bis beide Tasten losgelassen wurden; wiederholte Tastendrücke und anschließende erneute Betätigung sind als Kernprüfungen abgedeckt. Der echte Hardwaretest bleibt offen.
- Autostart erneut geprüft: `startWithWindows=false`, `fullscreen=true`, kein Benutzer-Run-Eintrag für ReaperArcade. Kein Neustart durchgeführt.
- Bibliothek nach weiterlaufender Migration erneut geprüft: um 08:20 waren 72 von 183 Startwegen dateiseitig vollständig. Das ist keine Spielbarkeitswertung.

Der Hook folgt der [Microsoft-Dokumentation zu LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc). Er wird beim Sitzungsende entfernt und protokolliert keine sonstigen Tasten.

## Migration

Die Hauptkopie war bei 816 von 1.668 Einträgen durch konkurrierenden Zugriff auf ihr Fortschrittsjournal abgebrochen. Sie wurde mit atomarem Schreiben, eigener Sicherungsdatei und Wiederholungen bei kurzzeitigen Dateisperren fortgesetzt. Um 08:12 wurden 1.134 fertige Einträge gemeldet. Vorhandene NAS- und Konfigurationsjobs bleiben erhalten und warten auf die Hauptkopie. Es wurde keine zweite gleichzeitige Hauptkopie gestartet.

## Softwareprüfungen bestanden

- React/TypeScript-Produktionsbuild und native Veröffentlichung für Windows x64 mit eigener .NET-Laufzeit.
- 50 Kernprüfungen mit Importdateien und Gegenfällen; zwei zusätzliche Prüfungen am tatsächlichen Übergabedatensatz: 183 eindeutige Spiele, 25 erste Prioritäten, 133 Videoverweise. Fehlende Dateien bleiben gesperrt, alternative vollständige Startwege werden bevorzugt, gemeinsame Sammlungsstarter bleiben getrennte Titel. MAME-Datenordner benötigen auch das passende ROM-Archiv.
- Abhängigkeits-Gegenfälle: falsche DLL-Architektur, erforderliche gegenüber verzögerten Imports, erneute Prüfung nach Ergänzen einer DLL, .NET-Version/Architektur, beschädigte PE-Datei sowie Pflichtabhängigkeit nach einem zuerst optional erreichten Pfad. Die Prüfung lädt oder führt die untersuchten Dateien nicht aus.
- Medien-URLs nur für vorhandene erlaubte Formate innerhalb der Medienordner; ausführbare Dateien, fremde Pfade und symbolische Links abgewiesen.
- Browserbedienung einschließlich Spielertrennung, Zieltest, Dateiauswahl, Tastatur und vier Ansichten bei fünf Bildschirmbreiten; erweiterte Bibliothek mit 183 Titeln, Filtern und Importaktion.
- Browser-Videotest mit einer echten MP4-Datei: Decodierung, Pause über kontrollierte Eingabe, Dialog und Spielsitzungsnachricht sowie Entfernen des Players beim Ansichtswechsel. Kontrollierte Eingaben sind kein physischer Gun-Nachweis.
- Das aktualisierte Installationsskript wurde vom Windows-PowerShell-Parser ohne Syntaxfehler gelesen und danach tatsächlich ausgeführt. `RemoteSigned` war vom Nutzer ausschließlich für den aktuellen Prozess freigegeben; keine dauerhafte Richtlinie geändert.

## Weiterhin offen

- Abschluss der Kopier-, NAS- und Konfigurationskette sowie anschließende erneute Bibliotheksprüfung.
- Weitere echte Spielstarts: TeknoParrot und die anderen Emulator-/Windows-Startwege. Ein vollständiger Dateipfad bestätigt keinen erfolgreichen Boot.
- Explizit konfigurierte Helfer einschließlich Start, Outputs, Portfreigabe und Beendigung. Das Helferfeld behauptet keinen automatisch fertigen Hook-of-the-Reaper-Pfad.
- RS3-ID, Kalibrierung, getrennte Spieler und einzelne Rückstoß-/Rumble-Impulse am lokalen Bildschirm; danach Spielefeedback.

Die Hardwareprüfung wartet auf angeschlossene Guns. Die Bibliothek vergibt keine erfundenen Spieler-, Rückstoß- oder Spielbarkeitsbestätigungen.
