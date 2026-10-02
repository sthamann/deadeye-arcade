# Abhängigkeiten

Reaper Arcade enthält eigene Oberfläche und eigenes Orchestrierungssystem. Emulatoren, Spiele und Gun-Helfer werden nicht mitgeliefert.

- React und React DOM: MIT, https://github.com/facebook/react
- Lucide: ISC, https://github.com/lucide-icons/lucide
- .NET Runtime / WPF: Microsoft-distribuierte Laufzeit, MIT und weitere Komponentenlizenzen; die Laufzeit-Lizenzdateien bleiben im Windows-Paket erhalten. https://github.com/dotnet/runtime und https://github.com/dotnet/wpf
- Microsoft Edge WebView2 SDK: Microsoft-Lizenz, https://www.nuget.org/packages/Microsoft.Web.WebView2 ; die separate WebView2 Runtime wird über Microsoft verteilt.
- System.IO.Ports und System.Security.Cryptography.ProtectedData: Microsoft .NET Pakete, MIT, https://github.com/dotnet/runtime
- Vite und TypeScript sind Entwicklungswerkzeuge und werden nicht als Windows-Prozesse ausgeliefert.

Eigene generische Cover-Platzhalter sind Bestandteil der Oberfläche. Echte Spielecover werden nur aus Nutzerdateien oder über den konfigurierten Metadatenanbieter geladen; sie unterliegen den Rechten ihrer jeweiligen Urheber.
