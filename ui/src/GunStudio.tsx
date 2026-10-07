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

const models = [
  {
    id: "rs3",
    name: "RS3 Reaper Pro",
    connection: "USB · 4 IR-Punkte",
    software: "Windows HID · kein Sondertreiber",
  },
  {
    id: "sinden",
    name: "Sinden Lightgun",
    connection: "USB · Kamera & Bildschirmrand",
    software: "Sinden Software 2.08b",
  },
  {
    id: "xgunner",
    name: "X-Gunner Wireless",
    connection: "2,4 GHz · USB-Empfänger",
    software: "X-Gunner Config V260808",
  },
  {
    id: "blamcon",
    name: "Blamcon Vyper",
    connection: "USB · IR-Tracking",
    software: "Blamcon ARC über Steam",
  },
];
const actions: Record<string, string> = {
  shoot: "Abzug / Schießen",
  reload: "Nachladen / Zurück",
  secondary: "Zweite Aktion",
  start: "Start / Bestätigen",
  coin: "Münze",
  up: "Nach oben",
  down: "Nach unten",
  left: "Nach links",
  right: "Nach rechts",
};
const points: Record<string, [number, number]> = {
  shoot: [254, 159],
  reload: [308, 121],
  secondary: [345, 121],
  start: [391, 116],
  coin: [426, 117],
  up: [326, 78],
  down: [326, 101],
  left: [314, 90],
  right: [338, 90],
};
const defaultMap = (player: number): Record<string, string> => ({
  "mouse:1": "shoot",
  "mouse:2": "reload",
  "mouse:3": "secondary",
  [`key:${48 + player}`]: "start",
  [`key:${52 + player}`]: "coin",
  "key:85": "up",
  "key:86": "down",
  "key:87": "left",
  "key:88": "right",
});
function tokenLabel(t: string) {
  return t.startsWith("mouse:")
    ? `Maustaste ${t.slice(6)}`
    : `Taste ${t.slice(4)}${+t.slice(4) >= 48 && +t.slice(4) <= 90 ? " · " + String.fromCharCode(+t.slice(4)) : ""}`;
}
export function GunShape({
  model = "rs3",
  mini = false,
  pressed = new Set<string>(),
  select,
}: {
  model?: string;
  mini?: boolean;
  pressed?: Set<string>;
  select?: (action: string) => void;
}) {
  return (
    <svg
      className={"gun-shape " + model + (mini ? " mini" : "")}
      viewBox="0 0 560 270"
      role="img"
      aria-label={
        mini ? "Lightgun-Silhouette" : "Schematische Lightgun mit Live-Tasten"
      }
    >
      <defs>
        <linearGradient id={"metal-" + model} x2="0" y2="1">
          <stop stopColor="#485366" />
          <stop offset=".47" stopColor="#202b3a" />
          <stop offset="1" stopColor="#0d1520" />
        </linearGradient>
      </defs>
      <g
        fill={`url(#metal-${model})`}
        stroke="currentColor"
        strokeWidth={mini ? 5 : 2}
        strokeLinejoin="round"
      >
        {model === "sinden" ? (
          <path d="M100 89H387L431 118 432 143H344L318 240H254L273 143H108Z" />
        ) : model === "blamcon" ? (
          <path d="M58 90H423L470 126V154H350L318 238H258L278 154H134L130 183H64Z" />
        ) : model === "xgunner" ? (
          <path d="M91 91H399L439 122V150H351L326 236H256L273 150H107Z" />
        ) : (
          <path d="M72 93H395L430 108H479L477 143H368L338 234H272L288 146H165L149 175H70Z" />
        )}
        <path d="M92 109H246V130H92Z" fill="#141e2a" />
        <path d="M170 89H379V79H181Z" />
        <path d="M272 149H328L316 183H284Z" fill="#0c1119" />
        <path d="M282 152L297 169 300 151" fill="none" />
        <path
          d="M303 193L330 201M296 207L327 215M291 220L323 230"
          opacity=".7"
        />
      </g>
      <path d="M177 99H371" stroke="#ff7850" strokeWidth="4" />
      {!mini && (
        <>
          <text x="178" y="122" fill="#8796ab" fontSize="12" letterSpacing="4">
            {model === "rs3" ? "REAPER" : model.toUpperCase()}
          </text>
          {Object.entries(points).map(([action, [x, y]]) => (
            <g
              key={action}
              className={
                "gun-hotspot " + (pressed.has(action) ? "pressed" : "")
              }
              onClick={() => select?.(action)}
              aria-hidden="true"
            >
              <circle cx={x} cy={y} r={action === "shoot" ? 15 : 8} />
            </g>
          ))}
          <path
            d="M254 162L225 212H175M394 116L435 55H495M311 120L269 45H179"
            fill="none"
            stroke="#516078"
            strokeWidth="1"
            strokeDasharray="4 5"
          />
          <text x="108" y="224" fill="#a5b3c7" fontSize="11">
            ABZUG
          </text>
          <text x="437" y="46" fill="#a5b3c7" fontSize="11">
            START / COIN
          </text>
          <text x="104" y="38" fill="#a5b3c7" fontSize="11">
            AKTIONEN
          </text>
        </>
      )}
    </svg>
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
      aria-label="Lightguns einrichten"
    >
      <span className="gun-header-count">
        <b>
          {count} {count === 1 ? "Gun" : "Guns"} erkannt
        </b>
        <small>{configured} softwareseitig eingerichtet</small>
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
            <b>P{p}</b>
            <GunShape model={gun?.systemId ?? binding?.systemId} mini />
            <span>
              <strong>
                {gun
                  ? models.find((m) => m.id === gun.systemId)?.name
                  : "Keine Gun"}
              </strong>
              <small>
                {!gun
                  ? "Nicht verbunden"
                  : gun.systemId === "xgunner" && !state.gunSignals?.[gun.id]
                    ? "Empfänger da · Gun prüfen"
                    : state.gunIssues?.[gun.id]
                      ? "Einrichtung prüfen"
                      : binding?.softwareConfigured
                        ? "Software bereit · Zieltest offen"
                        : "Einrichtung offen"}
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
  results: Record<number, { max: number; at: string }>;
}) {
  const [player, setPlayer] = useState(1);
  const binding = state.bindings.find((b) => b.player === player);
  const [model, setModel] = useState(binding?.systemId ?? "rs3");
  const [tab, setTab] = useState("buttons");
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
  const selected = models.find((m) => m.id === model)!;
  const map = binding?.buttonMap ?? defaultMap(player);
  const pressed = new Set(
    Object.keys(held)
      .filter((k) => held[k])
      .map((k) => map[k]),
  );
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
          <div className="eyebrow">LIGHTGUN STUDIO</div>
          <h1>Deine Gun. Dein Setup.</h1>
          <p>Einstecken, erkennen, Tasten ausprobieren – alles an einem Ort.</p>
        </div>
        <button
          className="secondary"
          disabled={busy}
          onClick={() => send("refresh")}
        >
          <RefreshCw size={18} />
          Neu erkennen
        </button>
      </div>
      <div className="system-cards">
        {models.map((m) => (
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
                P{p}
                <small>
                  {state.bindings.some(
                    (b) =>
                      b.player === p &&
                      (state.guns ?? []).some((g) => g.id === b.physicalId),
                  )
                    ? "Verbunden"
                    : "Nicht verbunden"}
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
              : `${selected.name} · Kein zugeordnetes Gerät`}
          </div>
        </div>
        <div className="studio-tabs">
          {[
            ["buttons", "Tasten & Live-Eingabe"],
            ["feedback", "Rückstoß & Vibration"],
            ["setup", "Einrichtung & Prüfung"],
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
              <div className="live-heading">
                <span className={"status-led " + (last ? "active" : "")} />
                <b>LIVE INPUT</b>
                <small>
                  {sameModel
                    ? gun?.liveInputAvailable
                      ? "Direkter Gun-Eingang"
                      : "USB erkannt · Eingabetest lokal"
                    : "Wartet auf diese Gun"}
                </small>
              </div>
              <GunShape
                model={model}
                pressed={sameModel ? pressed : new Set()}
                select={(action) =>
                  sameModel && send("learn-button", { player, action })
                }
              />
              <div className="signal-readout">
                <Crosshair size={18} />
                <span>
                  {sameModel && last
                    ? `Letzter Tastendruck: ${last}`
                    : "Betätige eine Taste an deiner Gun."}
                </span>
              </div>
              <p className="studio-caption">
                Schematische Ansicht. Die beleuchteten Punkte zeigen empfangene
                Tastenaktionen; die genaue Lage kann je nach Modell abweichen.
              </p>
              <div className="live-stats">
                <span>
                  <b>
                    {sameModel ? Object.values(seen).filter(Boolean).length : 0}
                  </b>
                  Tasten gesehen
                </span>
                <span>
                  <b>{sameModel ? (gun?.inputIds.length ?? 0) : 0}</b>
                  USB-Eingänge / eine Gun
                </span>
                <span>
                  <b>
                    {sameModel && binding?.softwareConfigured
                      ? "Bereit"
                      : "Offen"}
                  </b>
                  Software-Einrichtung
                </span>
              </div>
            </div>
            <div className="mapping-panel">
              <h2>Was macht welcher Knopf?</h2>
              <p>
                Aktion wählen, dann die gewünschte Taste an der Gun drücken.
              </p>
              {Object.entries(actions).map(([action, title]) => {
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
                        {tokens.map(tokenLabel).join(" / ") || "Nicht belegt"}
                      </small>
                    </span>
                    <span className="mapping-change">
                      {state.learning?.action === action &&
                      state.learning.player === player
                        ? "Taste drücken …"
                        : "Belegen"}
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
                  Belegung abbrechen
                </button>
              )}
              <p className="studio-caption">
                Diese Belegung steuert das Reaper-Menü. MAME übernimmt die
                unterstützten Spielaktionen beim Start; weitere Emulatoren
                verwenden ihre eigenen Profile. Start + Coin halten bleibt der
                unabhängige Notausgang.
              </p>
              <button
                className="text-button"
                disabled={!sameModel || busy}
                onClick={() => send("reset-button-map", { player })}
              >
                Standardbelegung wiederherstellen
              </button>
            </div>
          </div>
        )}
        {tab === "feedback" && (
          <div className="feedback-layout">
            <div className="feedback-visual">
              <GunShape model={model} />
              <Zap size={32} />
              <h2>Den Schuss spüren.</h2>
              <p>
                Ein einzelner Impuls zum Prüfen. Erst mit echten
                Spieleereignissen wird daraus ein passendes Schussfeedback.
              </p>
            </div>
            <div className="feedback-settings">
              <h2>{selected.name} · Feedback</h2>
              {model === "rs3" ? (
                <>
                  <div className="feedback-switch">
                    <span>
                      <strong>Rückstoß im Test</strong>
                      <small>Einzelner Solenoid-Impuls (Z5)</small>
                    </span>
                    <button
                      disabled={!canConfigure}
                      aria-label="Rückstoß im Test aktivieren"
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
                      <strong>Vibration im Test</strong>
                      <small>Einzelner Rumble-Impuls (ZZ)</small>
                    </span>
                    <button
                      disabled={!canConfigure}
                      aria-label="Vibration im Test aktivieren"
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
                    <b>Rückstoßkraft an der Gun</b>
                    <p>
                      <strong>SW4 OFF:</strong> normale Kraft ·{" "}
                      <strong>ON:</strong> reduzierte Kraft
                      <br />
                      <strong>SW3 OFF:</strong> normale Frequenz ·{" "}
                      <strong>ON:</strong> reduziert
                      <br />
                      <strong>SW5 OFF:</strong> ein Rückstoß pro Abzug
                      <br />
                      <strong>SW6 OFF:</strong> Vibration aktiv ·{" "}
                      <strong>ON:</strong> aus
                    </p>
                    <small>
                      Für mechanischen Rückstoß: vorgesehene 24-V-Versorgung
                      anschließen. USB allein reicht dafür nicht. Die RS3 bietet
                      keinen dokumentierten Software-Regler für die Kraft.
                    </small>
                  </div>
                  <div className="feedback-tests">
                    {[
                      ["recoil", "Rückstoß"],
                      ["rumble", "Vibration"],
                      ["combined", "Beides gekoppelt"],
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
                        {title} testen
                      </button>
                    ))}
                  </div>
                  <p className="studio-caption">
                    Gun dabei in der Hand halten. Unter Remote Desktop sind
                    Impulstests gesperrt. Test-Auswahl und DIP-Schalter schalten
                    kein spielabhängiges Feedback automatisch frei.
                  </p>
                </>
              ) : (
                <>
                  <p>
                    {model === "sinden"
                      ? "Sinden bietet softwareseitige Rückstoßstärke und Impulsmuster. Diese werden über die Hersteller-Software eingerichtet."
                      : model === "blamcon"
                        ? "Blamcon ARC bietet Rückstoßmodi und Rumble-Stärke. Für die Konfiguration muss die Blamcon angeschlossen sein."
                        : "Die X-Gunner wird über die offizielle Konfigurationssoftware eingerichtet. Funktionen hängen vom angeschlossenen Modell ab."}
                  </p>
                  <button
                    className="primary"
                    disabled={busy}
                    onClick={() => send("setup-gun", { system: model })}
                  >
                    Hersteller-Software vorbereiten / öffnen
                  </button>
                  <p className="studio-caption">
                    Für dieses System wurde hier noch kein reales Feedback
                    getestet.
                  </p>
                </>
              )}
            </div>
          </div>
        )}
        {tab === "setup" && (
          <div className="setup-layout">
            <section>
              <h2>Einrichtung für {selected.name}</h2>
              <div className="setup-progress">
                {[
                  [
                    !!sameModel,
                    "Gerät erkannt",
                    sameModel
                      ? gun!.name
                      : "USB-Gun bzw. Empfänger anschließen",
                  ],
                  [
                    !!sameModel && !!gun?.driverHealthy,
                    "Windows-Gerätestatus",
                    sameModel && gun?.driverHealthy
                      ? "Kein gemeldeter Gerätefehler"
                      : "Noch offen",
                  ],
                  [
                    !!sameModel && !!binding?.softwareConfigured,
                    "Software vorbereitet",
                    sameModel && binding?.softwareConfigured
                      ? "Mausmodus, Format und Reload angefordert"
                      : selected.software,
                  ],
                  [
                    !!sameModel && !!results[player],
                    "Zieltest am Bildschirm",
                    sameModel && results[player]
                      ? `${results[player].at} · max. ${results[player].max} px`
                      : "Fünf Ziele prüfen · lokal erforderlich",
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
                <p className="setup-warning">{state.gunIssues[gun.id]}</p>
              )}
              <div className="inline-actions">
                <button
                  className="primary"
                  disabled={busy}
                  onClick={() => send("setup-gun", { system: model })}
                >
                  <DownloadIcon />
                  {model === "rs3"
                    ? "Automatisch einrichten"
                    : "Software vorbereiten / öffnen"}
                </button>
                <button
                  className="secondary"
                  disabled={
                    state.native && (!sameModel || !!state.remoteSession)
                  }
                  onClick={() => test(player)}
                >
                  <Target size={18} />
                  Zieltest
                </button>
              </div>
              {model === "rs3" && (
                <>
                  <div className="feedback-switch">
                    <span>
                      <strong>Offscreen-Reload</strong>
                      <small>Außerhalb des Bildschirms nachladen</small>
                    </span>
                    <button
                      className={
                        "toggle " + (feedback.offscreenReload ? "on" : "")
                      }
                      disabled={!canConfigure}
                      aria-label="Offscreen-Reload aktivieren"
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
                    <span>Menü-Bildformat</span>
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
                    Spielstarts übernehmen das jeweilige Spielformat. Danach
                    stellt die App dein Menüformat wieder her. Die
                    Zielkalibrierung wird an deinem echten Bildschirm
                    vorgenommen.
                  </p>
                </>
              )}
              {model === "rs3" && (
                <div className="manufacturer-calibration">
                  <h3>Hersteller-Kalibrierung</h3>
                  <p>
                    Für die genaue Ausrichtung am echten Bildschirm das
                    vorhandene Retro-Shooter-Programm öffnen.
                  </p>
                  <div className="inline-actions">
                    <button
                      className="secondary"
                      disabled={busy}
                      onClick={() => send("set-calibration")}
                    >
                      Programm auswählen
                    </button>
                    <button
                      className="secondary"
                      disabled={
                        busy || !state.calibrationTool || !!state.remoteSession
                      }
                      onClick={() => send("run-calibration")}
                    >
                      Kalibrierung starten
                    </button>
                  </div>
                  {state.calibrationTool && (
                    <p className="studio-caption">{state.calibrationTool}</p>
                  )}
                </div>
              )}
              <div className="setup-note">
                <ShieldCheck size={24} />
                <p>
                  USB erkannt bedeutet noch keine geprüfte Zielgenauigkeit.
                  Stromversorgung, IR-Punkte bzw. Bildschirmrand und der reale
                  Zieltest gehören zur Einrichtung.
                </p>
              </div>
            </section>
            <section className="detected-panel">
              <h2>Erkannte Geräte</h2>
              {(state.guns ?? []).map((g) => (
                <div className="detected-gun" key={g.id}>
                  <GunShape model={g.systemId} mini />
                  <div>
                    <strong>{g.name}</strong>
                    <small>
                      {g.driverHealthy ? "Windows-Status OK" : "Gerätefehler"} ·{" "}
                      {g.port ?? "USB"} · {g.inputIds.length} Eingänge
                    </small>
                    <small>
                      {g.systemId === "xgunner"
                        ? "Empfänger erkannt; drahtlose Guns erst mit echten Eingaben prüfen"
                        : g.identityEvidence}
                    </small>
                  </div>
                  {g.systemId !== "rs3" && g.mouseId && (
                    <button
                      className="secondary compact"
                      onClick={() => send("assign-gun", { player, id: g.id })}
                    >
                      P{player} zuweisen
                    </button>
                  )}
                </div>
              ))}
              {!(state.guns ?? []).length && (
                <p>
                  Kein bekanntes Lightgun-Gerät gefunden. Normale Mäuse und
                  Tastaturen zählen hier nicht als Guns.
                </p>
              )}
              <h3>Herstellerprogramme</h3>
              <p>
                {model === "rs3"
                  ? "RS3 verwendet Windows HID und die serielle Schnittstelle; ein zusätzlicher Treiber ist nicht nötig."
                  : (state.gunSoftware?.find((s) => s.id === model)?.status ??
                    selected.software)}
              </p>
              <p className="studio-caption">
                Downloads kommen aus den Herstellerquellen und werden vor dem
                Öffnen anhand der geprüften Paket-Prüfsumme kontrolliert.
                Firmware-Updates werden nicht automatisch ausgeführt.
              </p>
              <details>
                <summary>Technische Eingänge anzeigen</summary>
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
