using System.Text.RegularExpressions;
namespace Reaper.Core;

public record GameCompatibilityInfo(string TwoPlayer,string[] Notes);
public static class GameCompatibility
{
    private static bool HasHelper(GameEntry game,string rom,string target="windows")=>(game.Helpers??[]).Any(h=>MultiplayerSetup.IsDemulShooter(h.Executable)&&h.Arguments.Contains("-target="+target)&&h.Arguments.Contains("-rom="+rom));
    public static GameCompatibilityInfo? Read(GameEntry game,int connectedPlayers)
    {
        string gunStatus=connectedPlayers<2?I18n.T("P2-Gun fehlt · gemeinsamer Spieltest offen"):I18n.T("Zwei Guns erkannt · gemeinsamer Spieltest offen");
        if(DolphinSetup.IsDolphin(game)) return new(I18n.T("P1-Profil beim Start · unabhängige P2-Konfiguration offen"),[
            I18n.T("Dolphin verwendet für P1 die gemeinsame Windows-Maus. P2 braucht eine eigene DirectInput-Gun samt geprüfter Belegung; ein GUN4IR-Profil passt nicht zur RS3."),
            game.Title.Contains("Dead Space",StringComparison.OrdinalIgnoreCase)?I18n.T("Dead Space: Standard-Wiimote mit Nunchuk wählen. Abzug: B; Magazin: A/Kinesis; Griff: Z/Nachladen; Seite: C/Stasis; Start: Pause; Coin kurz halten: Alternativfeuer; Stick klicken: Glow Worm; Griff + Seite: Nahkampf; Stickrichtungen: Waffenwahl."):I18n.T("USA-Titelprofile werden über die tatsächliche Disc-ID installiert. Andere Regionen behalten ihre vorhandene Belegung."),
            I18n.T("Bei RS3-P2 schaltet die App während Dolphin auf Joystick um, damit P2 nicht den P1-Mauszeiger bewegt. Das zweite Wiimote-Profil bleibt bis zur geprüften Einrichtung deaktiviert."),gunStatus]);
        if(game.Title.Contains("Blue Estate",StringComparison.OrdinalIgnoreCase))
        {
            bool installed=new[]{"BlueEstate_Fix.dll","MultiMouseLib.dll","d3d9.dll"}.All(n=>File.Exists(Path.Combine(game.WorkingDirectory,n)));
            return new(I18n.T(installed?"Zwei-Gun-Patch im Startordner · Geräteprüfung offen":"Zwei unabhängige Guns benötigen einen separaten Patch"),[
                I18n.T("Blue Estate benötigt den Patch für die passende 32-Bit-Spielversion, Raw Mode und Vollbild. P1/P2 werden beim Start aus den angeschlossenen Guns zugeordnet."),
                I18n.T("Der Patch verwendet VID/PID. Beide Guns benötigen unterschiedliche Kennungen; bei gleichen Kennungen wird der Start abgebrochen. Fokuswechsel und Hotplug während des Spiels werden vom Patch nicht unterstützt."),gunStatus]);
        }
        if(game.Title.Contains("Remake",StringComparison.OrdinalIgnoreCase)&&game.Title.Contains("Dead",StringComparison.OrdinalIgnoreCase))
        {
            if(Regex.IsMatch(game.Title,@"(?:DEAD|Dead)\s*2\s*:?\s*Remake",RegexOptions.IgnoreCase))
            {
                bool plugin=File.Exists(Path.Combine(game.WorkingDirectory,"BepInEx","plugins","MultiLightgunPlugin.dll"));
                return new(I18n.T(plugin?"MultiLightgunPlugin installiert · Gun-Zuweisung prüfen":"MultiLightgunPlugin fehlt im Startordner"),[
                    I18n.T("HOTD 2 Remake verwendet seinen eigenen MultiLightgunPlugin. Beide Guns am Zuweisungsbildschirm mit ihrem Abzug anmelden. Keinen HOTD-1-Patch und kein hotdra darüberlegen."),gunStatus]);
            }
            bool helper=HasHelper(game,"hotdra");
            return new(helper?I18n.T("DemulShooter-Startweg vorhanden · Plugin-Modus prüfen"):I18n.T("Zwei-Gun-Helfer fehlt im Startweg"),[
                I18n.T("ArcadePlugin für eine passende Spielversion benötigt. Für zwei Guns: MULTIPLAYER im Plugin und DemulShooterX64 -target=windows -rom=hotdra. SINGLEPLAYER verarbeitet nur eine Maus."),
                I18n.T("Windows-Skalierung 100 % und gleiche Spiel-/Desktopauflösung verwenden. Veraltete oder konkurrierende Plugins nicht gleichzeitig laden."),gunStatus]);
        }
        if(game.Title.Contains("Operation Wolf Returns",StringComparison.OrdinalIgnoreCase))return new(I18n.T(HasHelper(game,"opwolfr")?"DemulShooter-Startweg vorhanden · Plugin-Modus prüfen":"Zwei-Gun-Helfer fehlt im Startweg"),[
            I18n.T("Nicht-VR-Version benötigt den OperationWolf-DemulShooter-Plugin und DemulShooterX64 -target=windows -rom=opwolfr. Abzug, Nachladen und Waffenwechsel sind getrennt; P2-Granate wird im Plugin eingestellt."),gunStatus]);
        if(Path.GetFileName(game.Executable).StartsWith("emulator_",StringComparison.OrdinalIgnoreCase))return new(I18n.T((game.Helpers??[]).Any(h=>MultiplayerSetup.IsDemulShooter(h.Executable)&&h.Arguments.Contains("-target=model2"))?"Model 2 · DemulShooter für getrennte Guns":"Model 2 · Zwei-Gun-Helfer fehlt"),[
            I18n.T("Model 2 1.1a benötigt DemulShooter mit passendem ROM-Namen. Native RawInput-Eingabe und native Fadenkreuze werden für diesen Startweg deaktiviert. SERVICE-Kalibrierung am echten Bildschirm durchführen."),gunStatus]);
        if(HasHelper(game,"hod3pc")||HasHelper(game,"hod2pc"))return new(I18n.T("Windows-Klassiker · DemulShooter für getrennte Guns"),[
            I18n.T("Beide Spieler im Spiel bzw. Arcade-Launcher auf Keyboard einstellen. START: 1/2; Coin: 5. Die 32-Bit-Spielversion muss vom Helfer unterstützt werden."),gunStatus]);
        if(game.Title.Contains("Silent Hill",StringComparison.OrdinalIgnoreCase)) return new(game.Source=="teknoparrot"?I18n.T("TeknoParrot · getrennte RawInput-Guns beim Start"):I18n.T("Standalone-Version · No-Cursor-Startweg prüfen"),[
            game.Source=="teknoparrot"?I18n.T("Dieses Profil startet die TeknoParrot-Variante. Die App ordnet angeschlossene P1/P2-Guns per RawInput zu. Der Standalone-DemulShooter-Patch wird hier nicht darübergelegt."):I18n.T("Standalone-Anleitung benötigt die passende No-Cursor-EXE und DemulShooter -target=ttx -rom=sha. Keine SERVICE-Kalibrierung verwenden."),gunStatus]);
        if(SupermodelSetup.IsSupermodel(game))return new(I18n.T("Supermodel · getrennte RawInput-Geräte"),[I18n.T("Die App ermittelt Maus- und Tastaturnummern bei jedem Start neu und bindet P1/P2 getrennt. Unverbundene Spieler erhalten keine fremden Joystick-Eingänge."),gunStatus]);
        if(game.Source is "teknoparrot" or "mame")return new(I18n.T("Getrennte Gerätezuordnung beim Spielstart"),[I18n.T("Angeschlossene P1/P2-Guns werden dem passenden Profil zugeordnet. Spielunterstützung, Ingame-Start und tatsächliche Treffer bleiben separat zu testen."),gunStatus]);
        return null;
    }
}
