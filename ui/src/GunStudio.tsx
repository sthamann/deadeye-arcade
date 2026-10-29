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
import {GunMenuGuide} from "./GunMenuGuide";
import {BlamconPanel,BlamconGuide} from "./BlamconPanel";
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
    software: t("X-Gunner Config V261009"),
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
  auxiliary: t("Dritte Aktion"),
  mode: t("Modus / Waffenwechsel"),
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
  const count = guns.length;
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
        </small><small>{t("{0} aktiv · Spielerwahl",state.bindings.filter(b=>guns.some(g=>g.id===b.physicalId||g.mouseId===b.mouseId)).length)}</small>
      </span>
      {[1, 2].map((p) => {
        const binding = state.bindings.find((b) => b.player === p);
        const gun = guns.find(
          (g) => g.id === binding?.physicalId || g.mouseId === binding?.mouseId,
        );
        const okay =
          gun?.driverHealthy && !state.gunIssues?.[gun.id];
        return (
          <span
            key={p}
            className={"header-player " + (!okay ? "offline" : !state.gunSignals?.[gun!.id] ? "pending" : "online")}
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
              <small>{gun?.port && <span>{gun.port} · </span>}
                {!gun
                  ? t("Nicht verbunden")
                  : gun.systemId === "xgunner" && !state.gunSignals?.[gun.id]
                    ? t("Empf\u00E4nger da \u00B7 Gun pr\u00FCfen")
                    : state.gunIssues?.[gun.id]
                      ? t("Einrichtung pr\u00FCfen")
                      : binding?.softwareConfigured
                        ? state.gunSignals?.[gun.id] ? t("Tasteneingang empfangen · Zieltest offen") : t("USB bereit · Tastentest ausstehend")
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
  openRange,
}: {
  openRange: () => void;
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
  const softwareDependencies = state.gunSoftware?.find(s=>s.id===model)?.dependencies;
  const map = binding?.systemId===model ? binding.buttonMap ?? factoryMap(binding?.hardwarePlayer ?? player,model) : factoryMap(binding?.hardwarePlayer ?? player,model);
  const hardwarePlayer = binding?.hardwarePlayer ?? (binding?.systemId === "rs3" ? +(binding.mouseId.match(/PID_575([0-3])/i)?.[1] ?? (player - 1)) + 1 : player);
  const physicalControls = controls(model,hardwarePlayer);
  const tokenFor = (id:string) => (binding?.systemId===model ? binding.controlMap?.[id] : undefined) ?? (model==="blamcon" && (id==="magazine" || id==="pedal") ? ((binding?.blamcon?.pedalMode??"assignable")==="assignable"?"key:82":(binding?.blamcon?.pedalMode??"assignable")==="right"?"mouse:2":(binding?.blamcon?.pedalMode??"assignable")==="middle"?"mouse:3":undefined) : physicalControls.find(c=>c.id===id)?.token);
  const controlNames = (token:string) => physicalControls.filter(c=>tokenFor(c.id)===token).map(c=>t(c.label)).join(' / ') || tokenLabel(token);
  const unknownControls = online && binding?.systemId===model ? physicalControls.filter(c=>!c.hardware&&!tokenFor(c.id)) : [];
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
      <section className="gun-assignments">
        <div><h2>{t("Wer spielt mit welcher Gun?")}</h2><p>{t("Alle angeschlossenen Guns bleiben verfügbar. Wähle für P1 und P2 je eine Gun. Bereits belegte Plätze werden getauscht.")}</p></div>
        <div className="assignment-list">{(state.guns ?? []).map(g => {
          const slot=state.bindings.find(b=>b.physicalId===g.id || b.mouseId===g.mouseId)?.player;
          return <article className={"assignment-gun " + (slot ? "assigned" : "")} key={g.id}>
            <GunShape model={g.systemId} mini />
            <div><span className={"assignment-badge "+(slot?"active":"standby")}>{slot?t("Spielt als P{0}",slot):t("Wartet auf Einsatz")}</span><strong>{g.name}</strong><small>{g.port ?? "USB"} · {slot ? t("Aktiv für P{0}",slot) : t("Bereit zur Auswahl")}</small>
            {state.gunSoftware?.find(s=>s.id===g.systemId)?.status && <small className="software-progress">{message(state.gunSoftware.find(s=>s.id===g.systemId)!.status)}</small>}
            {g.systemId === "xgunner" && <small>{state.gunSignals?.[g.id] ? t("Eingabe empfangen") : t("Empfänger erkannt · Eingabetest offen")}</small>}</div>
            <div className="assignment-buttons">{[1,2].map(p=><button data-test-navigation key={p} className={slot===p ? "primary" : "secondary"} aria-pressed={slot===p} disabled={busy || !g.driverHealthy || !g.mouseId || slot===p} onClick={()=>{setPlayer(p);setModel(g.systemId);send("assign-gun",{player:p,id:g.id});}}>{t("P{0} verwenden",p)}</button>)}{slot && <button data-test-navigation className="secondary" disabled={busy} onClick={()=>send("unbind",{player:slot})}>{t("Nicht mitspielen")}</button>}</div>
          </article>;
        })}</div>
      </section>
      <GunMenuGuide binding={binding}/>
      {sameModel && unknownControls.length>0 && <div className="setup-note" role="status"><strong>{t("{0} Tasten noch erfassen",unknownControls.length)}</strong><p>{unknownControls.map(c=>t(c.label)).join(' · ')}</p><p>{t("USB ist verbunden. Diese Eingänge sind noch nicht bekannt und können erst nach dem Erfassen automatisch in Spielprofile übernommen werden.")}</p></div>}
      <div className="system-cards">
        {models().map((m) => (
          <button
            key={m.id}
            data-test-navigation
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
                data-test-navigation
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
        <button className="primary range-open" data-test-navigation onClick={openRange}><Target size={20}/>{t("Schießstand · 1–2 Spieler")}</button>
        <div className="studio-tabs">
          {[
            ["buttons", t("Tasten & Live-Eingabe")],
            ["feedback", t("R\u00FCcksto\u00DF & Vibration")],
            ["setup", t("Einrichtung & Pr\u00FCfung")],
          ].map(([id, name]) => (
            <button
              key={id}
              data-test-navigation
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
                <p>{buttonTesting ? t("Tasten werden nur angezeigt. Auf Spielerwechsel, Einrichtung oder „Tastentest beenden“ schießen, um weiterzugehen. Die andere Gun kann das Menü bedienen.") : t("Zum Belegen eine Zeile auswählen. Für reine Tastenanzeige den Tastentest starten.")}</p>
                <button className="secondary" data-test-navigation onClick={() => setButtonTesting(v => !v)}>{buttonTesting ? t("Tastentest beenden") : t("Tastentest starten")}</button>
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
              {sameModel && <section className="menu-learning">
                <button data-test-navigation className="primary" disabled={busy||!!state.learning} onClick={()=>send("learn-menu",{player})}>{t("Menütasten nacheinander einrichten")}</button>
                {state.learning?.player===player && state.learning.total && <div role="status"><h2>{t("Schritt {0} von {1}: {2}",(state.learning.step??0)+1,state.learning.total,t(physicalControls.find(c=>c.id===state.learning?.control)?.label??state.learning.control??""))}</h2><p>{t("Diese Taste drücken und wieder loslassen. Die nächste folgt automatisch. Reload bricht ab.")}</p><button data-test-navigation className="secondary" onClick={()=>send("cancel-learn")}>{t("Belegung abbrechen")}</button></div>}
              </section>}
              <div className="physical-controls">{physicalControls.map((control,i)=>{
                const token=tokenFor(control.id);
                return <button data-test-navigation key={control.id} className={"physical-row "+(pressed.has(control.id)?"pressed":"")} disabled={!sameModel||busy||control.hardware} onClick={()=>send("learn-control",{player,control:control.id})}>
                  <b>{i+1}</b><span><strong>{t(control.label)}</strong><small>{control.hardware?t("Hardware-Schalter · kein HID-Taster"):token?tokenLabel(token)+" · "+(actions()[map[token]]??t("Nicht belegt")):t("Eingang erfassen")}</small></span><span>{state.learning?.control===control.id?t("Taste drücken …"):control.hardware?"":t("Erfassen")}</span>
                </button>;
              })}</div>
              <p className="studio-caption">{model==="sinden"?t("Sinden: Bildschirmrand und Start/Stop bleiben Aufgaben der Herstellersoftware. Tasten können am Bildschirm und außerhalb verschiedene Eingänge senden."):model==="xgunner"?t("XGunner: Die Magazinentriegelung ist laut Anleitung mechanisch. Nachladen erfolgt über die rechte Maustaste oder Space; für die Entriegelung ist kein separater HID-Eingang dokumentiert."):model==="blamcon"?t("Vyper: Magazinzug = Nachladen, A = zweite Aktion, B = dritte Aktion. Magazin und optionales Pedal teilen den Anschluss. Der zweistufige Abzugsweg hat einen elektrischen Schusseingang."):t("Reaper: P1 Pfeiltasten / Q, P2 U/V/W/X / S. Münztaste vier Sekunden halten sendet Esc; beide Grifftasten teilen den Reload-Eingang.")}</p>
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
                        {tokens.map(controlNames).join(" / ") ||
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
                  "Die Grundaktionen werden beim Spielstart in unterstützte MAME-, TeknoParrot-RawInput-, Dolphin-, RPCS3-, RetroArch- und Supermodel-Profile übertragen. Zusätzliche Spielfunktionen bleiben auf eigenen Tasten. Spiele mit festem Maus-/Patch-Layout behalten ihre vorgegebene Belegung. Der Notausgang verwendet weiterhin die physischen Start- und Coin-Tasten.",
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
              ) : model === "blamcon" ? (
                <BlamconPanel binding={binding?.systemId===model?binding:undefined} player={player} connected={!!sameModel && !!gun?.port && gun.driverHealthy} busy={busy} remote={!!state.remoteSession} send={send}/>
              ) : model === "xgunner" ? (
                <>
                  <p>{t("Die XGunner unterstützt automatischen Rückstoß beim Schuss und Motorvibration außerhalb des Bildschirms. Der Vibration-Schalter an der Gun bleibt maßgeblich.")}</p>
                  <div className="feedback-tests">{["16:9","4:3"].map(aspect=><button className="secondary" key={aspect} disabled={busy || !sameModel || !gun?.port} onClick={()=>send("xgunner-mode",{player,aspect})}>{t("Lightgun-Modus {0}",aspect)}</button>)}</div>
                  <div className="feedback-tests">{[["recoil",t("Rückstoß testen")],["rumble",t("Vibration testen")]].map(([effect,title])=><button className="secondary" key={effect} disabled={busy || !sameModel || !gun?.port || !!state.remoteSession} onClick={()=>send("test-xgunner-feedback",{player,effect})}>{title}</button>)}</div>
                  <p>{t("Rapid Fire, Tasten, IR-Anordnung und Kalibrierung kannst du in der offiziellen XGunner-Software einstellen.")}</p>
                  <button className="primary" disabled={busy} onClick={()=>send("setup-gun",{system:"xgunner"})}>{t("XGunner-Konfiguration öffnen")}</button>
                  <p className="studio-caption">{t("Die Impulstests prüfen die Hardware. Feedback aus Spielereignissen benötigt zusätzlich das passende Spielprofil.")}</p>
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
            {model === "blamcon" && <BlamconGuide busy={busy} send={send}/>}
            {model === "xgunner" && <section className="xgunner-guide">
              <h2>{t("XGunner kalibrieren")}</h2>
              <ol><li>{t("Rapid Fire an der Gun ausschalten. Vier IR-Punkte passend zum gewählten Layout anbringen.")}</li><li>{t("Space und Start an der Gun gleichzeitig drücken. Dann einmal den Abzug drücken.")}</li><li>{t("Ziele in dieser Reihenfolge bestätigen: Mitte, oben, unten, links, rechts. Die Gun speichert die Kalibrierung anschließend selbst.")}</li></ol>
              <p>{t("Nach einem Wechsel zwischen Quadrat und Raute erneut kalibrieren. Q + E + A wählt die Raute, Q + E + D das Quadrat.")}</p>
              <button className="primary" disabled={busy} onClick={()=>send("setup-gun",{system:"xgunner"})}>{t("XGunner-Konfiguration öffnen")}</button>
            </section>}
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
                  {g.mouseId && (
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
              {softwareDependencies && <div className="setup-note" role="status"><strong>{softwareDependencies.missing?t("{0} benötigte Laufzeiten fehlen",softwareDependencies.missing):t("Herstellerprogramm: benötigte Laufzeiten geprüft")}</strong><p>{t("{0} Programmdateien geprüft",softwareDependencies.checkedBinaries)}</p>{softwareDependencies.uncheckedFiles>0&&<p>{t("{0} zusätzliche Abhängigkeiten separat prüfen",softwareDependencies.uncheckedFiles)}</p>}</div>}
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
