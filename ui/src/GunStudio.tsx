import { t, message } from "./i18n";
import { useEffect, useState } from "react";
import {
  Check,
  Crosshair,
  RefreshCw,
  ShieldCheck,
  Usb,
  Zap,
  Target,
  ArrowRight,
  X,
} from "lucide-react";
import type { State, GunSignal } from "./types";
import "./gun-studio.css";
import {GunShape, controls, factoryMap} from "./GunHardware";
export {GunShape} from "./GunHardware";
const models = () => [
  {
    id: "rs3",
    name: t("RS3 Reaper Pro"),
    connection: t("USB \u00B7 4 IR-Punkte"),
    software: t("Windows HID \u00B7 kein Sondertreiber"),
  },
  {
    id: "sinden",
    name: t("Sinden Lightgun"),
    connection: t("USB \u00B7 Kamera & Bildschirmrand"),
    software: t("Sinden Software 2.08b"),
  },
  {
    id: "xgunner",
    name: t("X-Gunner Wireless"),
    connection: t("2,4 GHz \u00B7 USB-Empf\u00E4nger"),
    software: t("X-Gunner Config V260808"),
  },
  {
    id: "blamcon",
    name: t("Blamcon Vyper"),
    connection: t("USB \u00B7 IR-Tracking"),
    software: t("Blamcon ARC \u00FCber Steam"),
  },
];
const actions = (): Record<string, string> => ({
  shoot: t("Abzug / Schie\u00DFen"),
  reload: t("Nachladen / Zur\u00FCck"),
  secondary: t("Zweite Aktion"),
  start: t("Start / Best\u00E4tigen"),
  coin: t("M\u00FCnze"),
  up: t("Nach oben"),
  down: t("Nach unten"),
  left: t("Nach links"),
  right: t("Nach rechts"),
});
function tokenLabel(token: string) {
  return token.startsWith("mouse:")
    ? t("Maustaste {0}", token.slice(6))
    : t(
        "Taste {0}{1}",
        token.slice(4),
        +token.slice(4) >= 48 && +token.slice(4) <= 90
          ? " · " + String.fromCharCode(+token.slice(4))
          : "",
      );
}
export function GunHeader({ state, open }: { state: State; open: () => void }) {
  const guns = state.guns ?? [];
  const count = guns.filter(
    (g) => g.systemId !== "xgunner" || state.gunSignals?.[g.id],
  ).length;
  const configured = state.bindings.filter(
    (b) =>
      b.softwareConfigured &&
      guns.some(
        (g) =>
          g.id === b.physicalId && g.driverHealthy && !state.gunIssues?.[g.id],
      ),
  ).length;
  return (
    <button
      className="gun-header"
      onClick={open}
      aria-label={t("Lightguns einrichten")}
    >
      <span className="gun-header-count">
        <b>
          {count} {count === 1 ? "Gun" : "Guns"}
          {" " + t("erkannt")}
        </b>
        <small>
          {configured}
          {" " + t("softwareseitig eingerichtet")}
        </small>
      </span>
      {[1, 2].map((p) => {
        const binding = state.bindings.find((b) => b.player === p);
        const gun = guns.find(
          (g) => g.id === binding?.physicalId || g.mouseId === binding?.mouseId,
        );
        const okay =
          gun?.driverHealthy &&
          (gun.systemId !== "xgunner" || !!state.gunSignals?.[gun.id]);
        return (
          <span
            key={p}
            className={"header-player " + (okay ? "online" : "offline")}
          >
            <b>
              {t("P")}
              {p}
            </b>
            <GunShape model={gun?.systemId ?? binding?.systemId} mini />
            <span>
              <strong>
                {gun
                  ? models().find((m) => m.id === gun.systemId)?.name
                  : t("Keine Gun")}
              </strong>
              <small>
                {!gun
                  ? t("Nicht verbunden")
                  : gun.systemId === "xgunner" && !state.gunSignals?.[gun.id]
                    ? t("Empf\u00E4nger da \u00B7 Gun pr\u00FCfen")
                    : state.gunIssues?.[gun.id]
                      ? t("Einrichtung pr\u00FCfen")
                      : binding?.softwareConfigured
                        ? t("Software bereit \u00B7 Zieltest offen")
                        : t("Einrichtung offen")}
              </small>
            </span>
          </span>
        );
      })}
    </button>
  );
}
export function GunStudio({
  state,
  signal,
  send,
  test,
  busy,
  results,
}: {
  state: State;
  signal: GunSignal | null;
  send: (type: string, payload?: unknown) => void;
  test: (p: number) => void;
  busy: boolean;
  results: Record<
    number,
    {
      max: number;
      at: string;
    }
  >;
}) {
  const [player, setPlayer] = useState(1);
  const binding = state.bindings.find((b) => b.player === player);
  const [model, setModel] = useState(binding?.systemId ?? "rs3");
  const [tab, setTab] = useState("buttons");
  const [buttonTesting, setButtonTesting] = useState(true);
  useEffect(() => {
    const end = () => setButtonTesting(false);
    window.addEventListener("deadeye-button-test-ended", end);
    return () => window.removeEventListener("deadeye-button-test-ended", end);
  }, []);
  useEffect(() => {
    send("button-test", {player:tab === "buttons" && buttonTesting ? player : null});
    return () => send("button-test", {player:null});
  }, [player, tab, buttonTesting]);
  const [held, setHeld] = useState<Record<string, boolean>>({});
  const [seen, setSeen] = useState<Record<string, boolean>>({});
  const [last, setLast] = useState("");
  useEffect(() => {
    if (binding?.systemId) setModel(binding.systemId);
    setHeld({});
    setSeen({});
    setLast("");
  }, [
    player,
    binding?.physicalId,
    (state.guns ?? []).some((g) => g.id === binding?.physicalId),
  ]);
  useEffect(() => {
    if (!signal || signal.player !== player) return;
    setHeld((v) => ({ ...v, [signal.token]: signal.down }));
    if (signal.down) {
      setSeen((v) => ({ ...v, [signal.token]: true }));
      setLast(tokenLabel(signal.token));
    }
  }, [signal, player]);
  const gun = (state.guns ?? []).find(
    (g) => g.id === binding?.physicalId || g.mouseId === binding?.mouseId,
  );
  const online = !!gun;
  const selected = models().find((m) => m.id === model)!;
  const map = binding?.systemId===model ? binding.buttonMap ?? factoryMap(player,model) : factoryMap(player,model);
  const physicalControls = controls(model,player);
  const tokenFor = (id:string) => (binding?.systemId===model ? binding.controlMap?.[id] : undefined) ?? physicalControls.find(c=>c.id===id)?.token;
  const pressed = new Set(physicalControls.filter(c=>{const token=tokenFor(c.id);return token&&held[token] || model === "rs3" && c.id === "trigger" && held["hid:1"];}).map(c=>c.id));
  const feedback = binding?.feedback ?? {
    recoil: true,
    rumble: true,
    offscreenReload: true,
    aspect: "16:9",
  };
  const updateFeedback = (value: Partial<typeof feedback>) =>
    send("set-gun-feedback", { player, feedback: { ...feedback, ...value } });
  const sameModel = online && binding?.systemId === model;
  const canConfigure =
    sameModel && model === "rs3" && !!binding?.serialPort && !busy;
  return (
    <div className="gun-studio">
      <div className="studio-heading">
        <div>
          <div className="eyebrow">{t("LIGHTGUN STUDIO")}</div>
          <h1>{t("Deine Gun. Dein Setup.")}</h1>
          <p>
            {t(
              "Einstecken, erkennen, Tasten ausprobieren \u2013 alles an einem Ort.",
            )}
          </p>
        </div>
        <button
          className="secondary"
          disabled={busy}
          onClick={() => send("refresh")}
        >
          <RefreshCw size={18} />
          {t("Neu erkennen")}
        </button>
      </div>
      <div className="system-cards">
        {models().map((m) => (
          <button
            key={m.id}
            className={"system-card " + (model === m.id ? "selected" : "")}
            onClick={() => setModel(m.id)}
          >
            <GunShape model={m.id} mini />
            <span>
              <strong>{m.name}</strong>
              <small>{m.connection}</small>
            </span>
            {model === m.id && <Check size={18} />}
          </button>
        ))}
      </div>
      <div className="studio-console">
        <div className="studio-toolbar">
          <div className="player-tabs">
            {[1, 2].map((p) => (
              <button
                key={p}
                className={player === p ? "selected" : ""}
                onClick={() => setPlayer(p)}
              >
                {t("P")}
                {p}
                <small>
                  {state.bindings.some(
                    (b) =>
                      b.player === p &&
                      (state.guns ?? []).some((g) => g.id === b.physicalId),
                  )
                    ? t("Verbunden")
                    : t("Nicht verbunden")}
                </small>
              </button>
            ))}
          </div>
          <div
            className={
              "studio-connection " + (sameModel ? "online" : "offline")
            }
          >
            <span className="status-led" />
            {sameModel
              ? `${gun?.name} · ${gun?.port ?? "USB"}`
              : t("{0} \u00B7 Kein zugeordnetes Ger\u00E4t", selected.name)}
          </div>
        </div>
        <div className="studio-tabs">
          {[
            ["buttons", t("Tasten & Live-Eingabe")],
            ["feedback", t("R\u00FCcksto\u00DF & Vibration")],
            ["setup", t("Einrichtung & Pr\u00FCfung")],
          ].map(([id, name]) => (
            <button
              key={id}
              className={tab === id ? "selected" : ""}
              onClick={() => setTab(id)}
            >
              {name}
            </button>
          ))}
        </div>
        {tab === "buttons" && (
          <div className="studio-body">
            <div className="gun-live-panel">
              <div className="setup-note button-test-note">
                <strong>{buttonTesting ? t("Tastentest · nur Anzeige") : t("Gun steuert das Menü")}</strong>
                <p>{buttonTesting ? t("Tasten werden nur angezeigt. Abzug 10 Sekunden halten und loslassen, um wieder das Menü zu bedienen. Mit der Maus kannst du den Test auch beenden.") : t("Zum Belegen eine Zeile auswählen. Für reine Tastenanzeige den Tastentest starten.")}</p>
                <button className="secondary" onClick={() => setButtonTesting(v => !v)}>{buttonTesting ? t("Tastentest beenden") : t("Tastentest starten")}</button>
                <p className="studio-caption">{t("Eigene Gun-Funktionen wie LED-Wechsel oder Maus-/Joystick-Umschaltung laufen in der Firmware und bleiben aktiv.")}</p>
              </div>
              <div className="live-heading">
                <span className={"status-led " + (last ? "active" : "")} />
                <b>{t("LIVE INPUT")}</b>
                <small>
                  {sameModel
                    ? gun?.liveInputAvailable
                      ? t("Direkter Gun-Eingang")
                      : t("USB erkannt \u00B7 Eingabetest lokal")
                    : t("Wartet auf diese Gun")}
                </small>
              </div>
              <GunShape
                model={model}
                pressed={sameModel ? pressed : new Set()}
                select={(control) => sameModel && send("learn-control", { player, control })}
              />
              <div className="signal-readout">
                <Crosshair size={18} />
                <span>
                  {sameModel && last
                    ? t("Letzter Tastendruck: {0}", last)
                    : t("Bet\u00E4tige eine Taste an deiner Gun.")}
                </span>
              </div>
              <p className="studio-caption">
                {t(
                  "Modellansicht nach Herstellerunterlagen. Nummern entsprechen den physischen Bedienelementen. Unbekannte Eingänge einmal erfassen; die Anzeige bleibt unabhängig von deiner Aktionsbelegung.",
                )}
              </p>
              <div className="physical-controls">{physicalControls.map((control,i)=>{
                const token=tokenFor(control.id);
                return <button key={control.id} className={"physical-row "+(pressed.has(control.id)?"pressed":"")} disabled={!sameModel||busy||control.hardware} onClick={()=>send("learn-control",{player,control:control.id})}>
                  <b>{i+1}</b><span><strong>{t(control.label)}</strong><small>{control.hardware?t("Hardware-Schalter · kein HID-Taster"):token?tokenLabel(token)+" · "+(actions()[map[token]]??t("Nicht belegt")):t("Eingang erfassen")}</small></span><span>{state.learning?.control===control.id?t("Taste drücken …"):control.hardware?"":t("Erfassen")}</span>
                </button>;
              })}</div>
              <p className="studio-caption">{model==="sinden"?t("Sinden: Bildschirmrand und Start/Stop bleiben Aufgaben der Herstellersoftware. Tasten können am Bildschirm und außerhalb verschiedene Eingänge senden."):model==="xgunner"?t("X-Gunner: Front-Stick und zwei Seitentasten nach Herstellerfotos. Firmware-Belegung wird an deinem Gerät erfasst."):model==="blamcon"?t("Vyper: Magazinzug, A/B, Stick und Start/Select. Feuerwahl steuert den Rückstoß am Gerät."):t("Reaper: P1 Pfeiltasten / Q, P2 U/V/W/X / S. Münztaste vier Sekunden halten sendet Esc; beide Grifftasten teilen den Reload-Eingang.")}</p>
              <div className="live-stats">
                <span>
                  <b>
                    {sameModel ? Object.values(seen).filter(Boolean).length : 0}
                  </b>
                  {t("Tasten gesehen")}
                </span>
                <span>
                  <b>{sameModel ? (gun?.inputIds.length ?? 0) : 0}</b>
                  {t("USB-Eing\u00E4nge / eine Gun")}
                </span>
                <span>
                  <b>
                    {sameModel && binding?.softwareConfigured
                      ? t("Bereit")
                      : t("Offen")}
                  </b>
                  {t("Software-Einrichtung")}
                </span>
              </div>
            </div>
            <div className="mapping-panel">
              <h2>{t("Was macht welcher Knopf?")}</h2>
              <p>
                {t(
                  "Aktion w\u00E4hlen, dann die gew\u00FCnschte Taste an der Gun dr\u00FCcken.",
                )}
              </p>
              {Object.entries(actions()).map(([action, title]) => {
                const tokens = Object.keys(map).filter(
                  (k) => map[k] === action,
                );
                const isHeld = sameModel && tokens.some((k) => held[k]);
                return (
                  <button
                    key={action}
                    disabled={!sameModel || busy}
                    className={
                      "mapping-row " +
                      (isHeld ? "pressed" : "") +
                      (state.learning?.player === player &&
                      state.learning.action === action
                        ? "learning"
                        : "")
                    }
                    onClick={() => send("learn-button", { player, action })}
                  >
                    <span className="mapping-indicator">
                      {isHeld ? <Zap size={17} /> : <span />}
                    </span>
                    <span>
                      <strong>{title}</strong>
                      <small>
                        {tokens.map(tokenLabel).join(" / ") ||
                          t("Nicht belegt")}
                      </small>
                    </span>
                    <span className="mapping-change">
                      {state.learning?.action === action &&
                      state.learning.player === player
                        ? t("Taste dr\u00FCcken \u2026")
                        : t("Belegen")}
                      <ArrowRight size={15} />
                    </span>
                  </button>
                );
              })}
              {state.learning?.player === player && (
                <button
                  className="secondary"
                  onClick={() => send("cancel-learn")}
                >
                  <X size={16} />
                  {t("Belegung abbrechen")}
                </button>
              )}
              <p className="studio-caption">
                {t(
                  "Diese Belegung steuert das Deadeye-Men\u00FC. MAME \u00FCbernimmt die unterst\u00FCtzten Spielaktionen beim Start; weitere Emulatoren verwenden ihre eigenen Profile. Außerhalb des Tastentests bleibt Start + Coin halten der unabhängige Notausgang.",
                )}
              </p>
              <button
                className="text-button"
                disabled={!sameModel || busy}
                onClick={() => send("reset-button-map", { player })}
              >
                {t("Standardbelegung wiederherstellen")}
              </button>
            </div>
          </div>
        )}
        {tab === "feedback" && (
          <div className="feedback-layout">
            <div className="feedback-visual">
              <GunShape model={model} />
              <Zap size={32} />
              <h2>{t("Den Schuss sp\u00FCren.")}</h2>
              <p>
                {t(
                  "Ein einzelner Impuls zum Pr\u00FCfen. Erst mit echten Spieleereignissen wird daraus ein passendes Schussfeedback.",
                )}
              </p>
            </div>
            <div className="feedback-settings">
              <h2>
                {selected.name}
                {" " + t("\u00B7 Feedback")}
              </h2>
              {model === "rs3" ? (
                <>
                  <div className="feedback-switch">
                    <span>
                      <strong>{t("R\u00FCcksto\u00DF im Test")}</strong>
                      <small>{t("Einzelner Solenoid-Impuls (Z5)")}</small>
                    </span>
                    <button
                      disabled={!canConfigure}
                      aria-label={t("R\u00FCcksto\u00DF im Test aktivieren")}
                      aria-pressed={feedback.recoil}
                      className={"toggle " + (feedback.recoil ? "on" : "")}
                      onClick={() =>
                        updateFeedback({ recoil: !feedback.recoil })
                      }
                    >
                      <span />
                    </button>
                  </div>
                  <div className="feedback-switch">
                    <span>
                      <strong>{t("Vibration im Test")}</strong>
                      <small>{t("Einzelner Rumble-Impuls (ZZ)")}</small>
                    </span>
                    <button
                      disabled={!canConfigure}
                      aria-label={t("Vibration im Test aktivieren")}
                      aria-pressed={feedback.rumble}
                      className={"toggle " + (feedback.rumble ? "on" : "")}
                      onClick={() =>
                        updateFeedback({ rumble: !feedback.rumble })
                      }
                    >
                      <span />
                    </button>
                  </div>
                  <div className="dip-card">
                    <b>{t("R\u00FCcksto\u00DFkraft an der Gun")}</b>
                    <p>
                      <strong>{t("SW4 OFF:")}</strong>
                      {" " + t("normale Kraft \u00B7")}{" "}
                      <strong>{t("ON:")}</strong>
                      {" " + t("reduzierte Kraft")}
                      <br />
                      <strong>{t("SW3 OFF:")}</strong>
                      {" " + t("normale Frequenz \u00B7")}{" "}
                      <strong>{t("ON:")}</strong>
                      {" " + t("reduziert")}
                      <br />
                      <strong>{t("SW5 OFF:")}</strong>
                      {" " + t("ein R\u00FCcksto\u00DF pro Abzug")}
                      <br />
                      <strong>{t("SW6 OFF:")}</strong>
                      {" " + t("Vibration aktiv \u00B7")}{" "}
                      <strong>{t("ON:")}</strong>
                      {" " + t("aus")}
                    </p>
                    <small>
                      {t(
                        "F\u00FCr mechanischen R\u00FCcksto\u00DF: vorgesehene 24-V-Versorgung anschlie\u00DFen. USB allein reicht daf\u00FCr nicht. Die RS3 bietet keinen dokumentierten Software-Regler f\u00FCr die Kraft.",
                      )}
                    </small>
                  </div>
                  <div className="feedback-tests">
                    {[
                      ["recoil", t("R\u00FCcksto\u00DF")],
                      ["rumble", t("Vibration")],
                      ["combined", t("Beides gekoppelt")],
                    ].map(([effect, title]) => (
                      <button
                        key={effect}
                        className={
                          effect === "combined" ? "primary" : "secondary"
                        }
                        disabled={
                          !canConfigure ||
                          !!state.remoteSession ||
                          ((effect === "recoil" || effect === "combined") &&
                            !feedback.recoil) ||
                          ((effect === "rumble" || effect === "combined") &&
                            !feedback.rumble)
                        }
                        onClick={() =>
                          send("test-feedback", { player, effect })
                        }
                      >
                        <Zap size={16} />
                        {title}
                        {" " + t("testen")}
                      </button>
                    ))}
                  </div>
                  <p className="studio-caption">
                    {t(
                      "Gun dabei in der Hand halten. Unter Remote Desktop sind Impulstests gesperrt. Test-Auswahl und DIP-Schalter schalten kein spielabh\u00E4ngiges Feedback automatisch frei.",
                    )}
                  </p>
                </>
              ) : (
                <>
                  <p>
                    {model === "sinden"
                      ? t(
                          "Sinden bietet softwareseitige R\u00FCcksto\u00DFst\u00E4rke und Impulsmuster. Diese werden \u00FCber die Hersteller-Software eingerichtet.",
                        )
                      : model === "blamcon"
                        ? t(
                            "Blamcon ARC bietet R\u00FCcksto\u00DFmodi und Rumble-St\u00E4rke. F\u00FCr die Konfiguration muss die Blamcon angeschlossen sein.",
                          )
                        : t(
                            "Die X-Gunner wird \u00FCber die offizielle Konfigurationssoftware eingerichtet. Funktionen h\u00E4ngen vom angeschlossenen Modell ab.",
                          )}
                  </p>
                  <button
                    className="primary"
                    disabled={busy}
                    onClick={() => send("setup-gun", { system: model })}
                  >
                    {t("Hersteller-Software vorbereiten / \u00F6ffnen")}
                  </button>
                  <p className="studio-caption">
                    {t(
                      "F\u00FCr dieses System wurde hier noch kein reales Feedback getestet.",
                    )}
                  </p>
                </>
              )}
            </div>
          </div>
        )}
        {tab === "setup" && (
          <div className="setup-layout">
            <section>
              <h2>
                {t("Einrichtung f\u00FCr") + " "}
                {selected.name}
              </h2>
              <div className="setup-progress">
                {[
                  [
                    !!sameModel,
                    t("Ger\u00E4t erkannt"),
                    sameModel
                      ? gun!.name
                      : t("USB-Gun bzw. Empf\u00E4nger anschlie\u00DFen"),
                  ],
                  [
                    !!sameModel && !!gun?.driverHealthy,
                    t("Windows-Ger\u00E4testatus"),
                    sameModel && gun?.driverHealthy
                      ? t("Kein gemeldeter Ger\u00E4tefehler")
                      : t("Noch offen"),
                  ],
                  [
                    !!sameModel && !!binding?.softwareConfigured,
                    t("Software vorbereitet"),
                    sameModel && binding?.softwareConfigured
                      ? t("Mausmodus, Format und Reload angefordert")
                      : selected.software,
                  ],
                  [
                    !!sameModel && !!results[player],
                    t("Zieltest am Bildschirm"),
                    sameModel && results[player]
                      ? t(
                          "{0} \u00B7 max. {1} px",
                          results[player].at,
                          results[player].max,
                        )
                      : t(
                          "F\u00FCnf Ziele pr\u00FCfen \u00B7 lokal erforderlich",
                        ),
                  ],
                ].map(([okay, title, detail], i) => (
                  <div key={i} className={okay ? "done" : ""}>
                    <span>{okay ? <Check size={19} /> : i + 1}</span>
                    <div>
                      <b>{title}</b>
                      <small>{detail}</small>
                    </div>
                  </div>
                ))}
              </div>
              {gun && state.gunIssues?.[gun.id] && (
                <p className="setup-warning">
                  {message(state.gunIssues[gun.id])}
                </p>
              )}
              <div className="inline-actions">
                <button
                  className="primary"
                  disabled={busy}
                  onClick={() => send("setup-gun", { system: model })}
                >
                  <DownloadIcon />
                  {model === "rs3"
                    ? t("Automatisch einrichten")
                    : t("Software vorbereiten / \u00F6ffnen")}
                </button>
                <button
                  className="secondary"
                  disabled={
                    state.native && (!sameModel || !!state.remoteSession)
                  }
                  onClick={() => test(player)}
                >
                  <Target size={18} />
                  {t("Zieltest")}
                </button>
              </div>
              {model === "rs3" && (
                <>
                  <div className="feedback-switch">
                    <span>
                      <strong>{t("Offscreen-Reload")}</strong>
                      <small>
                        {t("Au\u00DFerhalb des Bildschirms nachladen")}
                      </small>
                    </span>
                    <button
                      className={
                        "toggle " + (feedback.offscreenReload ? "on" : "")
                      }
                      disabled={!canConfigure}
                      aria-label={t("Offscreen-Reload aktivieren")}
                      aria-pressed={feedback.offscreenReload}
                      onClick={() =>
                        updateFeedback({
                          offscreenReload: !feedback.offscreenReload,
                        })
                      }
                    >
                      <span />
                    </button>
                  </div>
                  <div className="aspect-buttons">
                    <span>{t("Men\u00FC-Bildformat")}</span>
                    {["16:9", "4:3"].map((aspect) => (
                      <button
                        disabled={!canConfigure}
                        key={aspect}
                        className={feedback.aspect === aspect ? "selected" : ""}
                        onClick={() => updateFeedback({ aspect })}
                      >
                        {aspect}
                      </button>
                    ))}
                  </div>
                  <p className="studio-caption">
                    {t(
                      "Spielstarts \u00FCbernehmen das jeweilige Spielformat. Danach stellt die App dein Men\u00FCformat wieder her. Die Zielkalibrierung wird an deinem echten Bildschirm vorgenommen.",
                    )}
                  </p>
                </>
              )}
              {model === "rs3" && (
                <div className="manufacturer-calibration">
                  <h3>{t("Hersteller-Kalibrierung")}</h3>
                  <p>
                    {t(
                      "Vier Ziele direkt in Deadeye Arcade kalibrieren: Mitte, oben links, unten rechts, oben rechts. Nur die ausgewählte Gun schießt. Grifftaste oder Escape bricht ab.",
                    )}
                  </p>
                  <div className="inline-actions">
                    <button className="primary" disabled={busy || !sameModel || !!state.remoteSession} onClick={() => send("calibrate-rs3", {player})}>
                      <Crosshair size={18}/>{t("P{0} am Bildschirm kalibrieren",player)}
                    </button>
                    <button className="secondary" disabled={busy} onClick={()=>send("prepare-calibration")}>
                      {state.calibrationPrepared?t("Kalibrierung bereit"):t("Kalibrierung automatisch vorbereiten")}
                    </button>
                  </div>
                  <p className="studio-caption">{t("In 16:9 und deiner Spielposition kalibrieren. Für 4:3 genügt die automatische Formatumschaltung. Anschließend den Zieltest ausführen. Unter Remote Desktop ist nur die Vorbereitung verfügbar.")}</p>
                  <p className="studio-caption">{t("Die Kalibrierung sendet Herstellerbefehle direkt an die Gun. Es gibt keine laufende Deadeye-Mauskorrektur. Der Zieltest prüft nur die Genauigkeit; er verändert die Kalibrierung nicht.")}</p>
                  <details><summary>{t("Eigenes Herstellerprogramm verwenden")}</summary>
                  <div className="inline-actions">
                    <button
                      className="secondary"
                      disabled={busy}
                      onClick={() => send("set-calibration")}
                    >
                      {t("Programm ausw\u00E4hlen")}
                    </button>
                    <button
                      className="secondary"
                      disabled={
                        busy || !state.calibrationTool || !!state.remoteSession
                      }
                      onClick={() => send("run-calibration")}
                    >
                      {t("Kalibrierung starten")}
                    </button>
                  </div>
                  {state.calibrationTool && (
                    <p className="studio-caption">{state.calibrationTool}</p>
                  )}
                  </details>
                </div>
              )}
              <div className="setup-note">
                <ShieldCheck size={24} />
                <p>
                  {t(
                    "USB erkannt bedeutet noch keine gepr\u00FCfte Zielgenauigkeit. Stromversorgung, IR-Punkte bzw. Bildschirmrand und der reale Zieltest geh\u00F6ren zur Einrichtung.",
                  )}
                </p>
              </div>
            </section>
            <section className="detected-panel">
              <h2>{t("Erkannte Ger\u00E4te")}</h2>
              {(state.guns ?? []).map((g) => (
                <div className="detected-gun" key={g.id}>
                  <GunShape model={g.systemId} mini />
                  <div>
                    <strong>{g.name}</strong>
                    <small>
                      {g.driverHealthy
                        ? t("Windows-Status OK")
                        : t("Ger\u00E4tefehler")}{" "}
                      · {g.port ?? "USB"} · {g.inputIds.length}
                      {" " + t("Eing\u00E4nge")}
                    </small>
                    <small>
                      {g.systemId === "xgunner"
                        ? t(
                            "Empf\u00E4nger erkannt; drahtlose Guns erst mit echten Eingaben pr\u00FCfen",
                          )
                        : message(g.identityEvidence)}
                    </small>
                  </div>
                  {g.systemId !== "rs3" && g.mouseId && (
                    <button
                      className="secondary compact"
                      onClick={() => send("assign-gun", { player, id: g.id })}
                    >
                      {t("P")}
                      {player}
                      {" " + t("zuweisen")}
                    </button>
                  )}
                </div>
              ))}
              {!(state.guns ?? []).length && (
                <p>
                  {t(
                    "Kein bekanntes Lightgun-Ger\u00E4t gefunden. Normale M\u00E4use und Tastaturen z\u00E4hlen hier nicht als Guns.",
                  )}
                </p>
              )}
              <h3>{t("Herstellerprogramme")}</h3>
              <p>
                {model === "rs3"
                  ? t(
                      "RS3 verwendet Windows HID und die serielle Schnittstelle; ein zus\u00E4tzlicher Treiber ist nicht n\u00F6tig.",
                    )
                  : message(
                      state.gunSoftware?.find((s) => s.id === model)?.status ??
                        selected.software,
                    )}
              </p>
              <p className="studio-caption">
                {t(
                  "Downloads kommen aus den Herstellerquellen und werden vor dem \u00D6ffnen anhand der gepr\u00FCften Paket-Pr\u00FCfsumme kontrolliert. Firmware-Updates werden nicht automatisch ausgef\u00FChrt.",
                )}
              </p>
              <details>
                <summary>{t("Technische Eing\u00E4nge anzeigen")}</summary>
                {state.devices.map((d) => (
                  <p className="technical" key={d.id}>
                    {d.name} · {d.kind}
                    <br />
                    {d.id}
                  </p>
                ))}
              </details>
            </section>
          </div>
        )}
      </div>
    </div>
  );
}
function DownloadIcon() {
  return <Usb size={18} />;
}
