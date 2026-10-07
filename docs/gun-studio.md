# Lightgun Studio – Stand 7. Oktober 2026

Vier auswählbare Systeme: RS3 Reaper Pro, Sinden Lightgun, X-Gunner Wireless und Blamcon Vyper. Die Auswahl ist keine Bestätigung, dass dieses Modell angeschlossen oder erfolgreich getestet wurde.

## Erkennung und Spieler

Die Windows-Geräteabfrage gruppiert vorhandene Geräteschnittstellen über ihre Container-ID. Maus, Tastatur, Gamepad und COM-Port derselben USB-Gun zählen deshalb zusammen als ein Gerät. Ein generischer STM32-Vendor-Identifier reicht nicht zur Erkennung. Der Produktname muss zur unterstützten Herstellerfamilie gehören. Bei Blamcon bestätigt ein Familienname noch nicht das genaue Vyper-Modell. Bei X-Gunner bestätigt ein Empfänger noch keine aktive drahtlose Gun; echte Eingaben sind dafür nötig. Zwei Guns hinter einem gemeinsamen Empfänger sind noch kein verifizierter Erkennungspfad.

Bei RS3 fragt die App nur den zur identifizierten Gun gehörenden COM-Port ab. `ID` muss `id=1` oder `id=2` liefern. Maus- und Tastaturschnittstellen werden dem entsprechenden Spieler zugeordnet; belegte Spieler anderer angeschlossener Guns werden nicht überschrieben. P3/P4 werden erklärt, aber die Oberfläche unterstützt derzeit P1/P2. Normale Mäuse, Tastaturen und beliebige COM-Ports werden nicht als Guns übernommen.

Remote Desktop kann physische Eingänge aus der Raw-Input-Geräteliste ausblenden. Die App verwendet zusätzlich die realen Windows-Maus-, Tastatur- und HID-Geräteschnittstellen. Das ersetzt keinen empfangenen Tastendruck. Software-Einrichtung, Live-Eingabe und Zielgenauigkeit werden getrennt dargestellt. Nach USB-Änderungen wird die Erkennung erneut ausgeführt; während einer Spielsitzung wartet die Konfiguration bis zur Rückkehr ins Menü.

## Tasten

Empfangene Maus- und Tastaturmeldungen der zugeordneten Gun beleuchten die zugehörigen Aktionen in einer schematischen Zeichnung und der Tastenliste. Die Zeichnung ist kein maßstabgetreues Modell und keine Firmware-Sensordiagnose. Gamepad-only-Eingaben werden noch nicht dekodiert; RS3 wird für diesen Pfad in den dokumentierten Maus-/Tastaturmodus gesetzt.

Zum Belegen die gewünschte Aktion wählen und anschließend die gewünschte Gun-Taste drücken. Es werden ausschließlich Eingaben dieses Spielers übernommen. Die Belegung bleibt nach Neustart erhalten. Sie steuert die Menünavigation und erzeugt unterstützte MAME-Portbelegungen in der eigenen Controller-Datei beim Spielstart. MAME-Tasten außerhalb der unterstützten Buchstaben/Ziffern/Enter/Escape/Leertaste/Pfeiltasten werden derzeit nicht exportiert. TeknoParrot und weitere Emulatoren verwenden weiterhin ihre vorhandenen Controllerprofile; sie werden nicht pauschal überschrieben.

Der native Knopf „App schließen · Windows“ bleibt unabhängig von Browseransicht und Lernmodus bedienbar. Die ursprüngliche Start-plus-Coin-Kombination bleibt ein unabhängiger Notausgang und wird nicht durch die normale Menübelegung umprogrammiert.

## RS3-Einrichtung und Feedback

Die App prüft vor jedem seriellen Ablauf die Spieler-ID und wartet mindestens 120 ms zwischen Befehlen. Sie fordert Maus-/Tastaturmodus, 16:9 oder 4:3 und Offscreen-Reload an; anschließend endet der externe Modus. Das Menüformat wird nach einer Spielsitzung wiederhergestellt. Das Protokoll liefert für diese Schreibbefehle keine hier ausgewertete Zustandsbestätigung: „softwareseitig eingerichtet“ bedeutet Identität geprüft und dokumentierte Konfiguration angefordert.

Es wird beim Erkennen oder Einrichten kein Rückstoß ausgelöst. Lokale Testknöpfe senden einen einzelnen Rückstoß (`Z5`), einen Vibrationsimpuls (`ZZ`) oder die Kombination. Die beiden Feedback-Schalter wählen die zulässigen Testeffekte; sie ändern keine Hardware-DIP-Schalter und keine titelabhängigen Spieleoutputs. Unter Remote Desktop sind Impulstests gesperrt.

Die dokumentierte RS3-Kraftsteuerung sitzt an der Gun: SW4 OFF normale Kraft, ON reduziert. SW3 verändert die Frequenz. SW5 OFF löst einen Rückstoß pro Abzug aus; SW6 OFF aktiviert Vibration. Die vorgesehene 24-V-Stromversorgung wird für den mechanischen Rückstoß benötigt. Ein frei regelbarer Software-Kraftwert ist im dokumentierten RS3-Protokoll nicht vorhanden. Ein passendes Feedback für Treffer, leeres Magazin oder Dauerfeuer benötigt weiterhin den jeweiligen Spieloutput und dessen Helferkonfiguration.

## Herstellerprogramme

- **RS3:** Windows HID und USB-Serial; kein zusätzlicher Sondertreiber. Die App führt die oben genannten dokumentierten Konfigurationsschritte aus.
- **Sinden:** offizielles Windows-Paket 2.08b herunterladen, geprüfte SHA-256 kontrollieren, ausschließlich den Windows-Programmteil entpacken und Herstellerprogramm öffnen. Automatische Erkennung und Bildschirmrand sind vorbereitet. Hersteller-Lizenz- und Rückstoßbestätigungen bleiben unverändert. Kalibrierung und exakte Geräteauswahl werden in der Herstelleroberfläche geprüft.
- **X-Gunner:** offizielle Configuration Software V260808 herunterladen, geprüfte SHA-256 kontrollieren und Programm öffnen. Die modellspezifische Konfiguration läuft in diesem Programm; es gibt hier noch keinen verifizierten Schreibadapter dafür.
- **Blamcon:** die offizielle ARC-App über vorhandenes Steam öffnen oder deren Installation aufrufen (App 3324170). Steam-Anmeldung und gegebenenfalls Installationsbestätigung erfolgen in Steam. ARC-Geräteeinstellungen werden nicht ohne angeschlossene und geprüfte Blamcon geschrieben.

Bei einem neu erkannten Fremdherstellergerät wird der zugehörige Softwareweg einmal pro App-Lauf vorbereitet. Ein vorhandenes Sinden-/X-Gunner-Programm wird geöffnet, ohne eine bereits laufende Instanz aus demselben Ordner doppelt zu starten. Ein erkannter Maus-Eingang bekommt einen freien P1/P2-Platz; seine Gesamteinrichtung bleibt offen. Die Schaltfläche erlaubt den erneuten Aufruf. Ohne passendes Gerät wird keine erfolgreiche Gesamteinrichtung behauptet. Firmware wird nicht automatisch geflasht, Treibersignaturen und Windows-Schutzfunktionen werden nicht abgeschaltet.

## Tatsächlich geprüft am 7. Oktober 2026

Die Windows-Version wurde in einer Testumgebung installiert und gestartet. Eine angeschlossene RS3 wurde als ein physisches USB-Gerät mit drei Eingabeschnittstellen und einem COM-Port erkannt, ohne gemeldeten Treiberfehler. Die serielle Antwort bestätigte P1. Maus-/Tastatur-ID, Standardbelegung, Feedback-Testauswahl und Einrichtungsstatus wurden automatisch gespeichert und aus einem neuen App-Prozess wieder gelesen. Die Oberfläche zeigte P1 grün mit RS3-Modell und Softwarestatus, P2 rot. Das Schließen über den nativen Windows-Knopf wurde auch während des aktiven Tastenlernens durchgeführt.

58 Kernprüfungen bestehen, darunter Konfigurationsbefehle ohne Rückstoß beim Setup, Belegungsvalidierung, MAME-Ausgabe und gespeicherte Einstellungen. Browserprüfungen prüfen gedrückte/losgelassene Tasten mit ausdrücklich synthetischen Nachrichten, Lernanfragen, Feedback-Auswahl und die Oberfläche auf fünf Bildschirmbreiten. Sie bestätigen keinen realen Tastendruck der Gun.

Noch offen: echte Gun-Tastendrücke, Zielgenauigkeit, mechanischer Rückstoß, zwei gleichzeitig angeschlossene Guns und die drei weiteren Hersteller am realen Gerät. Die Remote-Sitzung blendete die RS3 aus Raw Input aus; die direkte Windows-Schnittstellenabfrage erkannte sie korrekt. Autostart bleibt deaktiviert.

## Quellen

- [RS3-Herstellerhandbuch, Februar 2026](https://retroshooter.com/wp-content/uploads/2026/02/Retro-Shooter-Reaper-User-Manual-2026.pdf)
- [Sinden-Herstellerdownloads](https://sindenlightgun.com/drivers/), inklusive Konfiguration und RecoilTcpServerReadme im offiziellen 2.08b-Paket
- [X-Gunner-Herstellerdownloads](https://hwhxg.com/downloads/)
- [Blamcon ARC-Handbuch](https://blamcon.com/get-started-with-blamcon/blamcon-arc-gui/) und [offizielle Steam-App](https://store.steampowered.com/app/3324170/Blamcon_ARC__Advanced_Remote_Console/)
- [MAME Controller-Konfiguration](https://docs.mamedev.org/advanced/ctrlr_config.html)
