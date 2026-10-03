# Erster Test auf dem Windows-PC

Ziel ist eine vollständige Runde: Einschalten → Menü → echte Gun → echtes Spiel → zurück ins Menü.

Der Softwaretest auf Windows ohne angeschlossene Guns einschließlich Programmstart und Menü-Rückkehr hat funktioniert. Für diese Runde werden echte Guns und ein vorhandenes Spiel benötigt. Autostart ist optional und lässt sich unter **Einstellungen** ein- und ausschalten.

## Vorbereiten

- Das vollständige 0.2-Paket entpacken und **ReaperArcade.exe** öffnen; bei bereits installierter App die vorhandene Verknüpfung verwenden.
- Für den ersten Test den Zielbildschirm als Windows-Hauptbildschirm verwenden. Mehrere Monitore und abweichende Skalierungen werden anschließend gezielt geprüft.
- RS3-Stromversorgung, USB und IR-Sensoren wie im Herstellerhandbuch anschließen. Unter **Meine Guns** die vorhandene Hersteller-Kalibrier-EXE auswählen und am lokalen Bildschirm ausführen.
- Guns in Maus-/Tastaturmodus verwenden. Spieler 1 muss Start `1` und Münze `5` senden; Spieler 2 `2` und `6`.
- Ein TeknoParrot-Lightgun-Spiel zunächst direkt im vorhandenen Emulator öffnen. Dieses funktionierende UserProfile wird importiert.

## Die entscheidende Runde

1. Spieler 1 und optional Spieler 2 über Abzug und Start zuordnen. Beide Zieleingänge müssen getrennt aufgeführt werden.
2. Bei Spieler 1 den Zieltest durchführen. Spieler 2 darf dessen Zieltest nicht abschließen können. Danach umgekehrt prüfen.
3. TeknoParrot-Ordner importieren und ein vorhandenes Spiel starten. Fehler wie fehlendes GamePath oder Basisprofil dürfen nicht als „bereit“ erscheinen.
4. Im Spiel Zielen, Abzug, Start, Münze, Reload und bei Bedarf Pedal prüfen. Zwei Spieler müssen unabhängig reagieren.
5. Auf der zugeordneten Gun Start + Münze etwa zwei Sekunden halten. Nur diese Spielsitzung soll beendet werden und das Menü zurückkehren.
6. Ein zweites Mal starten. Bei optionalem COM-Port prüfen, ob das gewählte Bildformat im Spiel und 16:9 im Menü stimmen.
7. PC neu starten. Bibliothek, Cover, Favoriten und Zuordnung müssen erhalten sein. USB neu anschließen; falls Windows die Gerätekennung ändert, muss die App „getrennt“ statt fälschlich „bereit“ zeigen und Neuzuordnung erlauben.
8. Ein MAME-Lightgun-Spiel importieren. Den tatsächlichen Analog- und Tastenpfad sowie die aus RawInput übernommenen Gerätekennungen im Emulator prüfen.

## Wenn etwas hakt

- Unter **Einstellungen → Diagnose speichern** eine JSON-Datei exportieren.
- `%LOCALAPPDATA%\ReaperArcade\activity.log` enthält Import-, Start- und Fehlermeldungen.
- Dokumentieren: Titel/Version, Emulatorversion, Windows-Skalierung, verwendeter Bildschirm, eine oder zwei Guns, erster fehlerhafter Schritt und sichtbare Fehlermeldung.
- Nicht „spielbar bestätigen“, solange der echte Spielpfad nicht funktioniert.
