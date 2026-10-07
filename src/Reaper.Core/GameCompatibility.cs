namespace Reaper.Core;

public record GameCompatibilityInfo(string TwoPlayer,string[] Notes);
public static class GameCompatibility
{
    public static GameCompatibilityInfo? Read(GameEntry game,int connectedPlayers)
    {
        string gunStatus=connectedPlayers<2?I18n.T("P2-Gun fehlt · gemeinsamer Spieltest offen"):I18n.T("Zwei Guns erkannt · gemeinsamer Spieltest offen");
        if(DolphinSetup.IsDolphin(game)) return new(I18n.T("P1-Profil beim Start · unabhängige P2-Konfiguration offen"),[
            I18n.T("Dolphin verwendet für P1 die gemeinsame Windows-Maus. P2 braucht eine eigene DirectInput-Gun samt geprüfter Belegung; ein GUN4IR-Profil passt nicht zur RS3."),
            game.Title.Contains("Dead Space",StringComparison.OrdinalIgnoreCase)?I18n.T("Dead Space: Standard-Wiimote mit Nunchuk wählen. Abzug: B; Magazin: A/Kinesis; Griff: Z/Nachladen; Seite: C/Stasis; Start: Pause; Coin kurz halten: Alternativfeuer; Stick klicken: Glow Worm; Griff + Seite: Nahkampf; Stickrichtungen: Waffenwahl."):I18n.T("USA-Titelprofile werden über die tatsächliche Disc-ID installiert. Andere Regionen behalten ihre vorhandene Belegung."),
            I18n.T("Bei RS3-P2 schaltet die App während Dolphin auf Joystick um, damit P2 nicht den P1-Mauszeiger bewegt. Das zweite Wiimote-Profil bleibt bis zur geprüften Einrichtung deaktiviert."),gunStatus]);
        if(game.Title.Contains("Blue Estate",StringComparison.OrdinalIgnoreCase)) return new(I18n.T("Zwei unabhängige Guns benötigen einen separaten Patch"),[
            I18n.T("Im Spiel Raw Mode aktivieren. Die unveränderte PC-Version verarbeitet nur eine Maus-Gun. Ein zweites Gerät allein schaltet keinen Zwei-Gun-Modus frei."),
            I18n.T("Die verlinkte Anleitung bietet den inoffiziellen Zwei-Gun-Patch nicht an. Seine Installation und Funktion sind hier nicht bestätigt."),gunStatus]);
        if(game.Title.Contains("Remake",StringComparison.OrdinalIgnoreCase)&&game.Title.Contains("Dead",StringComparison.OrdinalIgnoreCase))
        {
            bool helper=(game.Helpers??[]).Any(h=>Path.GetFileName(h.Executable).Equals("DemulShooterX64.exe",StringComparison.OrdinalIgnoreCase)&&h.Arguments.Contains("-target=windows")&&h.Arguments.Contains("-rom=hotdra"));
            return new(helper?I18n.T("DemulShooter-Startweg vorhanden · Plugin-Modus prüfen"):I18n.T("Zwei-Gun-Helfer fehlt im Startweg"),[
                I18n.T("ArcadePlugin für eine passende Spielversion benötigt. Für zwei Guns: MULTIPLAYER im Plugin und DemulShooterX64 -target=windows -rom=hotdra. SINGLEPLAYER verarbeitet nur eine Maus."),
                I18n.T("Windows-Skalierung 100 % und gleiche Spiel-/Desktopauflösung verwenden. Veraltete oder konkurrierende Plugins nicht gleichzeitig laden."),gunStatus]);
        }
        if(game.Title.Contains("Silent Hill",StringComparison.OrdinalIgnoreCase)) return new(game.Source=="teknoparrot"?I18n.T("TeknoParrot · getrennte RawInput-Guns beim Start"):I18n.T("Standalone-Version · No-Cursor-Startweg prüfen"),[
            game.Source=="teknoparrot"?I18n.T("Dieses Profil startet die TeknoParrot-Variante. Die App ordnet angeschlossene P1/P2-Guns per RawInput zu. Der Standalone-DemulShooter-Patch wird hier nicht darübergelegt."):I18n.T("Standalone-Anleitung benötigt die passende No-Cursor-EXE und DemulShooter -target=ttx -rom=sha. Keine SERVICE-Kalibrierung verwenden."),gunStatus]);
        if(SupermodelSetup.IsSupermodel(game))return new(I18n.T("Supermodel · getrennte RawInput-Geräte"),[I18n.T("Die App ermittelt Maus- und Tastaturnummern bei jedem Start neu und bindet P1/P2 getrennt. Unverbundene Spieler erhalten keine fremden Joystick-Eingänge."),gunStatus]);
        if(game.Source is "teknoparrot" or "mame")return new(I18n.T("Getrennte Gerätezuordnung beim Spielstart"),[I18n.T("Angeschlossene P1/P2-Guns werden dem passenden Profil zugeordnet. Spielunterstützung, Ingame-Start und tatsächliche Treffer bleiben separat zu testen."),gunStatus]);
        return null;
    }
}
