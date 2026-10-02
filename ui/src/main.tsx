import React, { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  Crosshair,
  Gamepad2,
  Library,
  Settings,
  Play,
  ArrowLeft,
  ArrowRight,
  Check,
  Plus,
  Usb,
  RefreshCw,
  Heart,
  Download,
  FolderOpen,
  X,
  Monitor,
  Target,
  ChevronRight,
  Power,
  Image as ImageIcon,
  AlertTriangle,
  Keyboard,
  ShieldCheck,
  Search,
  Info,
  Volume2,
} from "lucide-react";
import {
  empty,
  examples,
  badge,
  type Game,
  type State,
  type Input,
} from "./types";
import "./style.css";

type Page = "play" | "guns" | "import" | "settings";
type Modal =
  | { type: "game"; game: Game }
  | { type: "launch"; game: Game }
  | { type: "test"; player: number }
  | { type: "report"; count: number; warnings: string[] }
  | null;
const icons = {
  play: Gamepad2,
  guns: Crosshair,
  import: Library,
  settings: Settings,
};
const nav: [Page, string][] = [
  ["play", "Spielen"],
  ["guns", "Meine Guns"],
  ["import", "Spiele finden"],
  ["settings", "Einstellungen"],
];
const targets = [
  [0.5, 0.5],
  [0.12, 0.16],
  [0.88, 0.84],
  [0.88, 0.16],
  [0.12, 0.84],
];
const accents = [
  "#ff754f",
  "#60a5fa",
  "#88c990",
  "#f1b760",
  "#ca85e8",
  "#ff9374",
  "#66c8c2",
  "#9cabf2",
];

function App() {
  const [state, setState] = useState<State>(empty);
  const [page, setPage] = useState<Page>("play");
  const [demo, setDemo] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const [filter, setFilter] = useState("all");
  const [search, setSearch] = useState("");
  const [modal, setModal] = useState<Modal>(null);
  const [toast, setToast] = useState("");
  const [busy, setBusy] = useState("");
  const [session, setSession] = useState(false);
  const [coverKey, setCoverKey] = useState("");
  const [aim, setAim] = useState<{
    x: number;
    y: number;
    player: number;
  } | null>(null);
  const [lastInput, setLastInput] = useState<Input | null>(null);
  const [testStep, setTestStep] = useState(0);
  const [testErrors, setTestErrors] = useState<number[]>([]);
  const [testMisses, setTestMisses] = useState(0);
  const [testResults, setTestResults] = useState<
    Record<number, { max: number; at: string }>
  >({});
  const [justEnded, setJustEnded] = useState(false);
  const stateRef = useRef(state);
  const modalRef = useRef(modal);
  const stepRef = useRef(testStep);
  const errorsRef = useRef(testErrors);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const announce = (text: string) => {
    setToast(text);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setToast(""), 6500);
  };
  const send = (type: string, payload: unknown = {}) => {
    if (window.chrome?.webview)
      window.chrome.webview.postMessage({ type, payload });
    else
      announce(
        "Diese Funktion arbeitet in der Windows-App mit deinem PC. Hier siehst du die Bedienvorschau.",
      );
  };
  useEffect(() => {
    stateRef.current = state;
  }, [state]);
  useEffect(() => {
    modalRef.current = modal;
  }, [modal]);
  useEffect(() => {
    stepRef.current = testStep;
    errorsRef.current = testErrors;
  }, [testStep, testErrors]);
  useEffect(() => {
    if (timer.current)
      return () => {
        if (timer.current) clearTimeout(timer.current);
      };
  }, []);
  const handleShot = (x: number, y: number, player: number) => {
    const current = modalRef.current;
    if (current?.type !== "test" || stepRef.current >= 5) return;
    if (stateRef.current.native && player !== current.player) return;
    const target = targets[stepRef.current];
    let distance = Math.hypot(
      (x - target[0]) * window.innerWidth,
      (y - target[1]) * window.innerHeight,
    );
    if (distance > 68) {
      setTestMisses((n) => n + 1);
      return;
    }
    const next = [...errorsRef.current, distance];
    errorsRef.current = next;
    stepRef.current++;
    setTestErrors(next);
    setTestStep(stepRef.current);
    if (stepRef.current === 5 && stateRef.current.native)
      setTestResults((old) => ({
        ...old,
        [current.player]: {
          max: Math.round(Math.max(...next)),
          at: new Date().toLocaleTimeString("de-DE", {
            hour: "2-digit",
            minute: "2-digit",
          }),
        },
      }));
  };
  useEffect(() => {
    const bridge = window.chrome?.webview;
    if (!bridge) return;
    const receive = (event: { data: { type: string; payload: any } }) => {
      const { type, payload } = event.data;
      if (type === "state") {
        setState(payload);
        setDemo(false);
      }
      if (type === "error" || type === "notice") announce(payload.message);
      if (type === "busy") setBusy(payload.message);
      if (type === "import-result") {
        setModal({
          type: "report",
          count: payload.count,
          warnings: payload.warnings,
        });
        setPage("play");
      }
      if (type === "session") {
        setSession(payload.status === "running");
        if (payload.status === "ended") setJustEnded(true);
      }
      if (type === "input") {
        const input = payload as Input;
        setLastInput(input);
        if (
          input.kind === "mouse" &&
          input.x !== undefined &&
          input.y !== undefined
        ) {
          if (input.player > 0)
            setAim({ x: input.x, y: input.y, player: input.player });
          if ((input.buttons ?? 0) & 1) {
            if (modalRef.current?.type === "test")
              handleShot(input.x, input.y, input.player);
            else {
              const hit = document.elementFromPoint(
                input.x * window.innerWidth,
                input.y * window.innerHeight,
              );
              const button = hit?.closest("button") as HTMLButtonElement | null;
              if (button && !button.disabled) button.click();
              else if (
                hit instanceof HTMLInputElement ||
                hit instanceof HTMLSelectElement
              )
                hit.focus();
            }
          }
          if ((input.buttons ?? 0) & 4 && input.player > 0)
            document.dispatchEvent(
              new KeyboardEvent("keydown", { key: "Escape", bubbles: true }),
            );
        }
      }
    };
    bridge.addEventListener("message", receive);
    bridge.postMessage({ type: "ready" });
    // RawInput drives clicks in the native app; suppress duplicate legacy mouse clicks in the WebView.
    const legacy = (event: MouseEvent) => {
      if (event.isTrusted) {
        event.preventDefault();
        event.stopImmediatePropagation();
      }
    };
    document.addEventListener("click", legacy, true);
    return () => {
      bridge.removeEventListener("message", receive);
      document.removeEventListener("click", legacy, true);
    };
  }, []);
  useEffect(() => {
    const key = (event: KeyboardEvent) => {
      if (
        event.target instanceof HTMLInputElement ||
        event.target instanceof HTMLTextAreaElement ||
        event.target instanceof HTMLSelectElement
      )
        return;
      const map: Record<string, string> = {
        u: "ArrowUp",
        v: "ArrowDown",
        w: "ArrowLeft",
        x: "ArrowRight",
      };
      let k = event.key;
      if (state.native) k = map[k.toLowerCase()] ?? k;
      if (k === "Escape" || (state.native && k === "5")) {
        event.preventDefault();
        if (state.bindingStage) send("cancel-bind");
        else if (modal) setModal(null);
        else setPage("play");
        return;
      }
      if (
        state.native &&
        (k === "1" ||
          k === "2" ||
          k.toLowerCase() === "q" ||
          k.toLowerCase() === "s")
      )
        k = "Enter";
      if (
        k === "Enter" &&
        document.activeElement instanceof HTMLButtonElement
      ) {
        event.preventDefault();
        document.activeElement.click();
        return;
      }
      if (!k.startsWith("Arrow")) return;
      event.preventDefault();
      const buttons = Array.from(
        document.querySelectorAll<HTMLButtonElement>("button:not(:disabled)"),
      ).filter(
        (b) => b.getBoundingClientRect().width > 0 && !b.closest("[inert]"),
      );
      const active = document.activeElement as HTMLElement;
      const rect = active?.getBoundingClientRect();
      if (!rect || active === document.body) {
        buttons[0]?.focus();
        return;
      }
      const ax = rect.left + rect.width / 2,
        ay = rect.top + rect.height / 2;
      const options = buttons
        .filter((b) => b !== active)
        .map((b) => {
          const r = b.getBoundingClientRect();
          return {
            b,
            dx: r.left + r.width / 2 - ax,
            dy: r.top + r.height / 2 - ay,
          };
        })
        .filter((v) =>
          k === "ArrowRight"
            ? v.dx > 5
            : k === "ArrowLeft"
              ? v.dx < -5
              : k === "ArrowDown"
                ? v.dy > 5
                : v.dy < -5,
        );
      options.sort((a, b) => {
        const score = (v: typeof a) =>
          k === "ArrowLeft" || k === "ArrowRight"
            ? Math.abs(v.dx) + Math.abs(v.dy) * 3
            : Math.abs(v.dy) + Math.abs(v.dx) * 3;
        return score(a) - score(b);
      });
      options[0]?.b.focus();
    };
    document.addEventListener("keydown", key);
    return () => document.removeEventListener("keydown", key);
  }, [state.native, state.bindingStage, modal, page]);
  const allGames = demo ? examples : state.games;
  const games = allGames.filter(
    (g) =>
      (filter !== "favorites" || g.favorite) &&
      (filter !== "modern" || g.platform === "TeknoParrot") &&
      (filter !== "classic" || g.platform === "MAME") &&
      g.title.toLowerCase().includes(search.toLowerCase()),
  );
  const featured =
    allGames.find((g) => g.id === selected) ?? games[0] ?? allGames[0];
  const connected = (player: number) => {
    const binding = state.bindings.find((b) => b.player === player);
    return binding && state.devices.some((d) => d.id === binding.mouseId);
  };
  const connectedCount = state.bindings.filter((b) =>
    connected(b.player),
  ).length;
  const start = (g: Game) => {
    if (g.source === "demo") {
      announce(
        "Beispielspiel — importiere deine Installation, um es wirklich zu starten.",
      );
      return;
    }
    if (g.status === "needs-setup") {
      announce(
        "Der Spielpfad oder ein benötigtes Profil fehlt. Bitte in TeknoParrot korrigieren und erneut importieren.",
      );
      return;
    }
    setModal({ type: "launch", game: g });
  };
  const test = (player: number) => {
    setTestStep(0);
    stepRef.current = 0;
    setTestErrors([]);
    errorsRef.current = [];
    setTestMisses(0);
    setModal({ type: "test", player });
  };
  const action = (
    title: string,
    subtitle: string,
    icon: React.ReactNode,
    handler: () => void,
  ) => (
    <button className="action-tile" onClick={handler}>
      {icon}
      <div>
        <strong>{title}</strong>
        <span>{subtitle}</span>
      </div>
      <ChevronRight size={20} />
    </button>
  );
  return (
    <div className="arcade-app">
      <aside className="sidebar" inert={!!modal || !!state.bindingStage}>
        <button
          className="brand"
          onClick={() => setPage("play")}
          aria-label="Reaper Arcade Start"
        >
          <Crosshair size={30} />
          <span>
            REAPER<small>ARCADE</small>
          </span>
        </button>
        <div className="nav-label">DEINE ARCADE</div>
        <nav>
          {nav.map(([id, title]) => {
            const Icon = icons[id];
            return (
              <button
                key={id}
                aria-label={title}
                className={page === id ? "active" : ""}
                onClick={() => setPage(id)}
              >
                <Icon size={21} />
                <span>{title}</span>
                {page === id && <span className="nav-dot" />}
              </button>
            );
          })}
        </nav>
        <div className="sidebar-bottom">
          <div className="machine-label">
            <span className={connectedCount ? "dot good" : "dot"} />
            {state.native ? "Windows verbunden" : "Bedienvorschau"}
          </div>
          <span>Reaper Arcade · {state.version}</span>
          <button className="exit-link" onClick={() => send("close")}>
            <Power size={16} /> App schließen
          </button>
        </div>
      </aside>
      <div className="main-shell" inert={!!modal || !!state.bindingStage}>
        <header className="topbar">
          <div className="breadcrumb">
            DEIN SPIELZIMMER <span>/</span>{" "}
            {nav.find((n) => n[0] === page)?.[1].toUpperCase()}
          </div>
          <div className="top-status">
            <button className="device-pill" onClick={() => setPage("guns")}>
              <Usb size={15} />
              {connectedCount
                ? `${connectedCount} Gun${connectedCount > 1 ? "s" : ""} verbunden`
                : "Guns einrichten"}
              <ChevronRight size={13} />
            </button>
            <span className="version-chip">EARLY ACCESS</span>
          </div>
        </header>
        {!state.native && (
          <div className="preview-strip">
            <Info size={16} />
            <span>
              Bedienvorschau im Browser. Geräte, Dateiimport und Spielstart
              arbeiten in der Windows-App.
            </span>
          </div>
        )}
        {demo && (
          <div className="preview-strip demo-strip">
            <ImageIcon size={16} />
            <span>
              Beispielbibliothek · diese Spiele sind hier nicht installiert
            </span>
            <button
              onClick={() => {
                setDemo(false);
                setSelected(null);
              }}
            >
              Vorschau verlassen <X size={14} />
            </button>
          </div>
        )}
        <main>
          {page === "play" && (
            <>
              {featured ? (
                <>
                  <section
                    className="hero"
                    style={
                      {
                        "--game-accent":
                          accents[allGames.indexOf(featured) % accents.length],
                      } as React.CSSProperties
                    }
                  >
                    <div className="hero-text">
                      <div className="eyebrow">
                        <span className="small-line" />
                        {featured.platform.toUpperCase()} <span>·</span>{" "}
                        {badge(featured).toUpperCase()}
                      </div>
                      <h1>{featured.title}</h1>
                      <p>
                        {featured.platform === "MAME"
                          ? "Die Klassiker. Dein Ziel. Deine Arcade."
                          : "Große Arcade-Action. Direkt in deinem Spielzimmer."}
                      </p>
                      <div className="hero-meta">
                        <span>
                          <Monitor size={15} /> {featured.aspect}
                        </span>
                        <span>
                          <Crosshair size={15} /> Lightgun-Profil
                        </span>
                      </div>
                      <div className="hero-buttons">
                        <button
                          className="primary"
                          onClick={() => start(featured)}
                          disabled={!!busy}
                        >
                          <Play size={20} fill="currentColor" />
                          {featured.source === "demo"
                            ? "Startablauf ansehen"
                            : "Spiel starten"}
                        </button>
                        <button
                          className="secondary"
                          onClick={() =>
                            setModal({ type: "game", game: featured })
                          }
                        >
                          Spieldetails <ArrowRight size={17} />
                        </button>
                      </div>
                    </div>
                    <div className="hero-art" aria-hidden="true">
                      {featured.cover ? (
                        <img src={featured.cover} alt="" />
                      ) : (
                        <>
                          <div className="art-grid" />
                          <div className="orbit orbit-one" />
                          <div className="orbit orbit-two" />
                          <div className="art-cross" />
                          <div className="art-number">
                            {featured.title.includes("Time Crisis")
                              ? "05"
                              : featured.title.includes("Jurassic")
                                ? "JP"
                                : featured.title.includes("Point")
                                  ? "PB"
                                  : "RS"}
                          </div>
                          <div className="art-caption">
                            AIM FOR SOMETHING GREAT
                          </div>
                        </>
                      )}
                    </div>
                  </section>
                  <div className="library-toolbar">
                    <div>
                      <h2>
                        Deine Spiele <span>{games.length}</span>
                      </h2>
                      <p>
                        {demo
                          ? "So könnte deine Sammlung aussehen."
                          : "Importiert. An einem Ort. Bereit für den nächsten Spieltest."}
                      </p>
                    </div>
                    <div className="library-tools">
                      <label className="search-box">
                        <Search size={16} />
                        <input
                          aria-label="Spiele suchen"
                          placeholder="Spiel suchen"
                          value={search}
                          onChange={(e) => setSearch(e.target.value)}
                        />
                      </label>
                      <button
                        className="icon-button"
                        onClick={() => setPage("import")}
                        aria-label="Spiele importieren"
                      >
                        <Plus size={21} />
                      </button>
                    </div>
                  </div>
                  <div className="filters">
                    {[
                      ["all", "Alle Spiele"],
                      ["modern", "Modern Arcade"],
                      ["classic", "Klassiker"],
                      ["favorites", "Favoriten"],
                    ].map(([id, title]) => (
                      <button
                        key={id}
                        className={filter === id ? "active" : ""}
                        onClick={() => setFilter(id)}
                      >
                        {title}
                      </button>
                    ))}
                  </div>
                  <div className="game-grid">
                    {games.map((game, index) => (
                      <button
                        key={game.id}
                        className={
                          "game-tile " +
                          (featured.id === game.id ? "selected" : "")
                        }
                        aria-label={game.title + " auswählen"}
                        onClick={() => setSelected(game.id)}
                        style={
                          {
                            "--game-accent": accents[index % accents.length],
                          } as React.CSSProperties
                        }
                      >
                        <div className="poster">
                          {game.cover ? (
                            <img
                              src={game.cover}
                              alt={game.title}
                              onError={(e) => {
                                e.currentTarget.style.display = "none";
                              }}
                            />
                          ) : (
                            <>
                              <div className="poster-ring" />
                              <div className="poster-mark">
                                <Crosshair size={34} />
                              </div>
                              <span className="poster-title">{game.title}</span>
                              <span className="poster-kicker">
                                {game.platform.toUpperCase()}
                              </span>
                            </>
                          )}
                          <span
                            className={
                              "poster-status " +
                              (game.status === "tested" ? "verified" : "")
                            }
                          >
                            {game.favorite ? (
                              <Heart size={12} fill="currentColor" />
                            ) : (
                              <span className="tiny-dot" />
                            )}
                            {badge(game)}
                          </span>
                        </div>
                        <div className="game-caption">
                          <strong>{game.title}</strong>
                          <span>{game.platform}</span>
                        </div>
                      </button>
                    ))}
                  </div>
                  {games.length === 0 && (
                    <div className="no-results">
                      Keine Spiele in dieser Auswahl.
                    </div>
                  )}
                </>
              ) : (
                <section className="welcome">
                  <div className="welcome-art">
                    <div className="orbit" />
                    <Crosshair size={105} />
                    <span>
                      YOUR ARCADE
                      <br />
                      STARTS HERE.
                    </span>
                  </div>
                  <div className="welcome-content">
                    <div className="eyebrow">WILLKOMMEN IN DEINER ARCADE</div>
                    <h1>
                      Guns nehmen.
                      <br />
                      Spiele starten.
                    </h1>
                    <p>
                      Wir verbinden deine Lightguns und bringen deine Spiele in
                      eine gemeinsame Bibliothek.
                    </p>
                    <div className="welcome-steps">
                      <span>
                        <b>01</b> Guns zuordnen
                      </span>
                      <span>
                        <b>02</b> Spiele importieren
                      </span>
                      <span>
                        <b>03</b> Loslegen
                      </span>
                    </div>
                    <div className="hero-buttons">
                      <button
                        className="primary"
                        onClick={() => setPage("guns")}
                      >
                        <Crosshair size={19} /> Guns einrichten
                      </button>
                      <button
                        className="secondary"
                        onClick={() => setPage("import")}
                      >
                        <Plus size={18} /> Spiele hinzufügen
                      </button>
                    </div>
                    <button
                      className="text-button"
                      onClick={() => {
                        setDemo(true);
                        setSelected(null);
                      }}
                    >
                      Mit Beispielspielen ansehen <ArrowRight size={15} />
                    </button>
                  </div>
                </section>
              )}
              {justEnded && (
                <div className="after-game">
                  <Check size={18} />
                  <span>
                    Zurück in deiner Arcade. Wenn alles funktioniert hat, kannst
                    du das Spiel in den Details als spielbar bestätigen.
                  </span>
                  <button
                    onClick={() => setJustEnded(false)}
                    aria-label="Hinweis schließen"
                  >
                    <X size={16} />
                  </button>
                </div>
              )}
            </>
          )}
          {page === "guns" && (
            <>
              <div className="page-heading">
                <div className="eyebrow">DEIN SETUP</div>
                <h1>
                  Gute Kontrolle.
                  <br />
                  Ab dem ersten Schuss.
                </h1>
                <p>
                  Jede Gun bekommt ihren eigenen Spieler. Anschluss, Eingaben
                  und Zieltest werden getrennt geprüft.
                </p>
              </div>
              <div className="gun-grid">
                {[1, 2].map((player) => {
                  const binding = state.bindings.find(
                    (b) => b.player === player,
                  );
                  const online = connected(player);
                  return (
                    <section className="gun-panel" key={player}>
                      <div className="gun-card-header">
                        <div className="player-icon">
                          <Crosshair size={28} />
                        </div>
                        <div>
                          <span className="eyebrow">SPIELER {player}</span>
                          <h2>
                            {binding ? "Deine Lightgun" : "Gun hinzufügen"}
                          </h2>
                        </div>
                        <span
                          className={
                            online ? "connection-tag online" : "connection-tag"
                          }
                        >
                          {online
                            ? "Angeschlossen"
                            : binding
                              ? "Getrennt"
                              : "Nicht zugeordnet"}
                        </span>
                      </div>
                      <div className="gun-illustration" aria-hidden="true">
                        <div className="gun-barrel" />
                        <div className="gun-slide" />
                        <div className="gun-grip" />
                        <div className="gun-trigger" />
                        <div className="gun-leds">● ● ● ● ●</div>
                      </div>
                      <div className="check-row">
                        <span>
                          <Usb size={17} /> USB-Eingang
                        </span>
                        <b>{online ? "Vorhanden" : "Offen"}</b>
                      </div>
                      <div className="check-row">
                        <span>
                          <Keyboard size={17} /> Tasten zugeordnet
                        </span>
                        <b>{binding?.keyboardId ? "Ja" : "Offen"}</b>
                      </div>
                      <div className="check-row">
                        <span>
                          <Target size={17} /> Zieltest dieser Sitzung
                        </span>
                        <b>
                          {testResults[player] && online
                            ? `${testResults[player].at} · max. ${testResults[player].max} px`
                            : "Noch offen"}
                        </b>
                      </div>
                      <div className="check-row">
                        <span>
                          <Volume2 size={17} /> Recoil & Pedal
                        </span>
                        <b>Spieltest nötig</b>
                      </div>
                      <div className="gun-actions">
                        <button
                          className="primary"
                          onClick={() => send("bind", { player })}
                        >
                          {binding ? "Neu zuordnen" : "Gun zuordnen"}
                          <Plus size={17} />
                        </button>
                        <button
                          className="secondary"
                          disabled={state.native && !online}
                          onClick={() => test(player)}
                        >
                          <Target size={17} /> Zieltest
                        </button>
                      </div>
                      {binding && (
                        <details className="device-details">
                          <summary>RS3-Port und Geräteinformationen</summary>
                          <p>
                            Optional: den COM-Port dieser Gun auswählen. Die App
                            prüft zuerst die RS3-ID und fordert nur Mausmodus
                            und Bildformat an.
                          </p>
                          <label>
                            Gun-COM-Port
                            <select
                              aria-label={`COM-Port Spieler ${player}`}
                              value={binding.serialPort ?? ""}
                              onChange={(e) =>
                                send("set-port", {
                                  player,
                                  port: e.target.value,
                                })
                              }
                            >
                              <option value="">Nicht zugeordnet</option>
                              {state.ports.map((port) => (
                                <option key={port}>{port}</option>
                              ))}
                            </select>
                          </label>
                          <div className="inline-actions">
                            <button
                              className="secondary"
                              onClick={() => send("test-serial", { player })}
                            >
                              Verbindung prüfen
                            </button>
                            <button
                              className="secondary"
                              onClick={() => send("mouse-mode", { player })}
                            >
                              Mausmodus setzen
                            </button>
                          </div>
                          <p className="technical">{binding.mouseId}</p>
                          <button
                            className="text-button danger"
                            onClick={() => send("unbind", { player })}
                          >
                            Zuordnung entfernen
                          </button>
                        </details>
                      )}
                    </section>
                  );
                })}
              </div>
              <div className="setup-note">
                <ShieldCheck size={22} />
                <div>
                  <strong>Erkannt ist noch nicht vollständig geprüft.</strong>
                  <p>
                    Der Zieltest prüft die Eingabe an fünf Zielen. Er ersetzt
                    keine Firmwarekalibrierung. Recoil und Pedal werden aktuell
                    im Spiel geprüft.
                  </p>
                </div>
              </div>
              <div className="section-heading">
                <h2>Angeschlossene Eingänge</h2>
                <button
                  className="secondary compact"
                  onClick={() => send("refresh")}
                >
                  <RefreshCw size={16} /> Neu erkennen
                </button>
              </div>
              <div className="device-list">
                {state.devices.length ? (
                  state.devices.map((d) => (
                    <div key={d.id}>
                      <Usb size={18} />
                      <span>
                        {d.name}
                        <small>
                          {d.kind === "mouse"
                            ? "Maus / Zieleingang"
                            : d.kind === "keyboard"
                              ? "Tasteneingang"
                              : "Controller / HID"}
                          {d.retroShooter ? " · Retro-Shooter-Familie" : ""}
                        </small>
                      </span>
                    </div>
                  ))
                ) : (
                  <p>
                    {state.native
                      ? "Keine Eingänge gemeldet. Gun anschließen und neu erkennen."
                      : "In der Browser-Vorschau wird keine Hardware erkannt."}
                  </p>
                )}
              </div>
              {lastInput && (
                <div className="input-live">
                  Letztes Signal: Spieler{" "}
                  {lastInput.player || "nicht zugeordnet"} · {lastInput.kind}{" "}
                  {lastInput.key !== undefined
                    ? `· Taste ${lastInput.key}`
                    : ""}
                </div>
              )}
            </>
          )}
          {page === "import" && (
            <>
              <div className="page-heading">
                <div className="eyebrow">EINE GEMEINSAME BIBLIOTHEK</div>
                <h1>
                  Deine Spiele.
                  <br />
                  Ein Startpunkt.
                </h1>
                <p>
                  Wähle deine vorhandenen Installationen. Spieldateien bleiben
                  an ihrem bisherigen Ort.
                </p>
              </div>
              <div className="import-grid">
                {action(
                  "TeknoParrot",
                  "Vorhandene Lightgun-Spielprofile automatisch einlesen",
                  <Crosshair size={30} />,
                  () => send("import-tekno"),
                )}
                {action(
                  "MAME",
                  "Lightgun-Titel aus dem Emulator-Katalog und deinen ROMs erkennen",
                  <Gamepad2 size={30} />,
                  () => send("import-mame"),
                )}
                {action(
                  "Windows-Spiel",
                  "Eine vorhandene Spielanwendung zur Bibliothek hinzufügen",
                  <Monitor size={30} />,
                  () => send("add-pc"),
                )}
                {action(
                  "Cover ergänzen",
                  state.settings.hasCoverKey
                    ? "Eindeutige Cover automatisch laden"
                    : "Eigene Bilder verwenden oder API-Schlüssel hinterlegen",
                  <ImageIcon size={30} />,
                  () =>
                    state.settings.hasCoverKey
                      ? send("fetch-covers")
                      : setPage("settings"),
                )}
              </div>
              <div className="import-explainer">
                <span className="eyebrow">WAS BEIM IMPORT PASSIERT</span>
                <div className="import-flow">
                  {[
                    "Spiele finden",
                    "Profil zuordnen",
                    "Cover ergänzen",
                    "Im Spiel prüfen",
                  ].map((title, i) => (
                    <div key={title}>
                      <b>0{i + 1}</b>
                      <strong>{title}</strong>
                      {i < 3 && <ArrowRight size={16} />}
                    </div>
                  ))}
                </div>
                <p>
                  TeknoParrot nutzt bereits angelegte UserProfiles. MAME liefert
                  seine Lightgun-Liste selbst. Fehlende ROM-Bestandteile und
                  Spieleinstellungen werden erst beim tatsächlichen Spieltest
                  sichtbar.
                </p>
              </div>
              <div className="coming-next">
                <strong>Nächste Anbindungen</strong>
                <span>
                  DuckStation · PCSX2 · Dolphin · Flycast · Model 2 · Supermodel
                  · Steam
                </span>
                <p>
                  Diese Plattformen sind in Version 0.1 noch nicht
                  implementiert.
                </p>
              </div>
            </>
          )}
          {page === "settings" && (
            <>
              <div className="page-heading">
                <div className="eyebrow">DEIN ARCADE-PC</div>
                <h1>
                  Einmal einstellen.
                  <br />
                  Entspannt spielen.
                </h1>
              </div>
              <section className="settings-panel">
                <div className="setting-row">
                  <div>
                    <strong>Direkt im Vollbild starten</strong>
                    <p>Große Oberfläche für deinen Bildschirm.</p>
                  </div>
                  <button
                    className={
                      "toggle " + (state.settings.fullscreen ? "on" : "")
                    }
                    role="switch"
                    aria-checked={state.settings.fullscreen}
                    aria-label="Vollbild"
                    onClick={() =>
                      send("fullscreen", {
                        enabled: !state.settings.fullscreen,
                      })
                    }
                  >
                    <span />
                  </button>
                </div>
                <div className="setting-row">
                  <div>
                    <strong>Mit Windows starten</strong>
                    <p>Öffnet die App nach deiner Windows-Anmeldung.</p>
                  </div>
                  <button
                    className={
                      "toggle " + (state.settings.startWithWindows ? "on" : "")
                    }
                    role="switch"
                    aria-checked={state.settings.startWithWindows}
                    aria-label="Autostart mit Windows"
                    onClick={() =>
                      send("autostart", {
                        enabled: !state.settings.startWithWindows,
                      })
                    }
                  >
                    <span />
                  </button>
                </div>
                <div className="setting-block">
                  <div className="setting-title">
                    <ImageIcon size={20} />
                    <strong>Automatische Spielecover</strong>
                    {state.settings.hasCoverKey && (
                      <span className="connection-tag online">
                        Schlüssel gespeichert
                      </span>
                    )}
                  </div>
                  <p>
                    Optionaler SteamGridDB-API-Schlüssel. Er wird unter deinem
                    Windows-Benutzer verschlüsselt gespeichert. Alternativ
                    kannst du jedem Spiel ein lokales Cover geben.
                  </p>
                  <label className="key-entry">
                    <input
                      type="password"
                      autoComplete="off"
                      placeholder="SteamGridDB-API-Schlüssel"
                      aria-label="SteamGridDB API Schlüssel"
                      value={coverKey}
                      onChange={(e) => setCoverKey(e.target.value)}
                    />
                    <button
                      className="secondary"
                      onClick={() => {
                        send("set-cover-key", { key: coverKey });
                        setCoverKey("");
                      }}
                    >
                      Speichern
                    </button>
                  </label>
                  <div className="inline-actions">
                    <button
                      className="primary"
                      onClick={() => send("fetch-covers")}
                      disabled={!state.settings.hasCoverKey}
                    >
                      <Download size={17} /> Fehlende Cover laden
                    </button>
                    {state.settings.hasCoverKey && (
                      <button
                        className="text-button"
                        onClick={() => send("set-cover-key", { key: "" })}
                      >
                        Schlüssel entfernen
                      </button>
                    )}
                  </div>
                </div>
                <div className="setting-block">
                  <div className="setting-title">
                    <Info size={20} />
                    <strong>Diagnose für den ersten Windows-Test</strong>
                  </div>
                  <p>
                    Speichert Gerätekennungen, Zuordnungen und Spielezustände.
                    API-Schlüssel werden nicht exportiert.
                  </p>
                  <button
                    className="secondary"
                    onClick={() => send("export-diagnostics")}
                  >
                    <Download size={17} /> Diagnose speichern
                  </button>
                </div>
              </section>
              <div className="setup-note">
                <Keyboard size={22} />
                <div>
                  <strong>Zurück aus dem Spiel</strong>
                  <p>
                    Auf der zugeordneten Gun Start + Münze für etwa zwei
                    Sekunden halten. Spieler 1: Tasten 1 + 5, Spieler 2: 2 + 6.
                    Die App beobachtet die Kombination im Hintergrund.
                  </p>
                </div>
              </div>
            </>
          )}
        </main>
        <footer className="footer-controls">
          <span>
            <b>
              <Crosshair size={14} />
            </b>{" "}
            Zielen & Abzug: auswählen
          </span>
          <span>
            <b>↔</b> Stick: navigieren
          </span>
          <span>
            <b>↩</b> Zurück: eine Ebene zurück
          </span>
          <span className="footer-right">MADE FOR YOUR GAME ROOM</span>
        </footer>
      </div>
      {busy && (
        <div className="busy-bar" role="status">
          <RefreshCw size={17} />
          {busy}
        </div>
      )}
      {toast && (
        <div className="toast" role="status">
          <Info size={19} />
          <span>{toast}</span>
          <button onClick={() => setToast("")} aria-label="Hinweis schließen">
            <X size={17} />
          </button>
        </div>
      )}
      {aim && aim.x >= 0 && aim.x <= 1 && aim.y >= 0 && aim.y <= 1 && (
        <div
          className={"aim-cursor player-" + aim.player}
          style={{ left: aim.x * 100 + "%", top: aim.y * 100 + "%" }}
          aria-hidden="true"
        >
          <Crosshair size={30} />
          <span>P{aim.player}</span>
        </div>
      )}
      {state.bindingStage && (
        <div className="modal-backdrop">
          <section
            className="modal bind-modal"
            role="dialog"
            aria-modal="true"
            aria-label="Gun zuordnen"
          >
            <span className="eyebrow">
              SPIELER {state.bindingStage.player} ZUORDNEN
            </span>
            <Crosshair className="bind-icon" size={66} />
            <h2>
              {state.bindingStage.stage === "trigger"
                ? "Drücke den Abzug."
                : "Drücke jetzt Start."}
            </h2>
            <p>
              {state.bindingStage.stage === "trigger"
                ? "Nimm die Gun für diesen Spieler und schieße einmal. So erkennen wir ihren eigenen Zieleingang."
                : "Drücke die Start-Taste an derselben Gun. Damit ordnen wir auch ihren Tasteneingang zu."}
            </p>
            <div className="wizard-progress">
              <span className="done">1 · Zieleingang</span>
              <ChevronRight size={16} />
              <span
                className={state.bindingStage.stage === "start" ? "done" : ""}
              >
                2 · Tasten
              </span>
            </div>
            <button className="secondary" onClick={() => send("cancel-bind")}>
              Abbrechen
            </button>
          </section>
        </div>
      )}
      {modal && modal.type !== "test" && (
        <div className="modal-backdrop">
          <section
            className="modal"
            role="dialog"
            aria-modal="true"
            aria-label={
              modal.type === "report" ? "Importergebnis" : modal.game.title
            }
          >
            <button
              className="modal-close"
              onClick={() => setModal(null)}
              aria-label="Dialog schließen"
            >
              <X size={22} />
            </button>
            {modal.type === "report" ? (
              <>
                <span className="eyebrow">IMPORT ABGESCHLOSSEN</span>
                <h2>{modal.count} Spiele eingelesen.</h2>
                <p>
                  Vorhandene Einträge wurden aktualisiert. Favoriten und eigene
                  Cover bleiben erhalten.
                </p>
                {modal.warnings.length > 0 && (
                  <details>
                    <summary>{modal.warnings.length} Hinweise ansehen</summary>
                    <ul className="warning-list">
                      {modal.warnings.map((w, i) => (
                        <li key={i}>{w}</li>
                      ))}
                    </ul>
                  </details>
                )}
                <button className="primary" onClick={() => setModal(null)}>
                  Zur Bibliothek <ArrowRight size={18} />
                </button>
              </>
            ) : modal.type === "launch" ? (
              <>
                <span className="eyebrow">
                  {modal.game.platform.toUpperCase()} · ERSTER START
                </span>
                <h2>{modal.game.title}</h2>
                <p>
                  Die App startet dein gespeichertes Profil. Ob Zielen, Tasten,
                  Pedal und zwei Spieler im Spiel funktionieren, prüfen wir auf
                  deinem PC.
                </p>
                <div className="launch-checks">
                  <span>
                    <Check size={16} /> Vorhandenen Starter verwenden
                  </span>
                  <span>
                    <Check size={16} /> RS3-Bildformat setzen, wenn ein COM-Port
                    zugeordnet ist
                  </span>
                  <span>
                    <Check size={16} /> Nach Spielende ins Menü zurückkehren
                  </span>
                </div>
                <div className="exit-reminder">
                  <Keyboard size={19} />
                  <span>
                    Start + Münze etwa 2 Sekunden halten, um das Spiel zu
                    beenden.
                  </span>
                </div>
                <button
                  className="primary"
                  onClick={() => {
                    send("launch", { id: modal.game.id });
                    setModal(null);
                  }}
                >
                  <Play size={18} /> Jetzt starten
                </button>
              </>
            ) : (
              <>
                <span className="eyebrow">
                  {modal.game.platform.toUpperCase()} ·{" "}
                  {badge(modal.game).toUpperCase()}
                </span>
                <h2>{modal.game.title}</h2>
                <div className="detail-actions">
                  <button className="primary" onClick={() => start(modal.game)}>
                    <Play size={18} /> Spiel starten
                  </button>
                  <button
                    className="secondary"
                    onClick={() => send("favorite", { id: modal.game.id })}
                  >
                    <Heart size={17} /> Favorit umschalten
                  </button>
                </div>
                <div className="setting-row">
                  <strong>Bildformat der Gun</strong>
                  <div className="segmented">
                    {["16:9", "4:3"].map((aspect) => (
                      <button
                        className={modal.game.aspect === aspect ? "active" : ""}
                        key={aspect}
                        onClick={() => {
                          send("set-aspect", { id: modal.game.id, aspect });
                          setModal({
                            type: "game",
                            game: { ...modal.game, aspect },
                          });
                        }}
                      >
                        {aspect}
                      </button>
                    ))}
                  </div>
                </div>
                <div className="inline-actions">
                  <button
                    className="secondary"
                    onClick={() => send("add-cover", { id: modal.game.id })}
                  >
                    <ImageIcon size={17} /> Eigenes Cover wählen
                  </button>
                  <button
                    className="secondary"
                    onClick={() => {
                      send("mark-tested", { id: modal.game.id });
                      setModal(null);
                    }}
                  >
                    <Check size={17} /> Als spielbar bestätigen
                  </button>
                </div>
                <p className="detail-note">
                  „Spielbar bestätigt“ bedeutet, dass du das Spiel mit deinem
                  Setup geprüft hast.
                </p>
                <button
                  className="text-button danger"
                  onClick={() => {
                    send("remove-game", { id: modal.game.id });
                    setModal(null);
                  }}
                >
                  Aus Bibliothek entfernen
                </button>
              </>
            )}
          </section>
        </div>
      )}
      {modal?.type === "test" && (
        <div
          className="target-test"
          role="dialog"
          aria-modal="true"
          aria-label="Zieltest"
        >
          <div className="target-test-header">
            <div>
              <span className="eyebrow">
                SPIELER {modal.player} ·{" "}
                {state.native ? "EINGABETEST" : "BEDIENPROBE"}
              </span>
              <h2>
                {testStep < 5
                  ? "Triff das leuchtende Ziel."
                  : "Fünf Ziele getroffen."}
              </h2>
              <p>
                {state.native
                  ? "Nur Eingaben der zugeordneten Gun werden gewertet."
                  : "Vorschau mit Maus oder Touch — keine Geräteprüfung."}
              </p>
            </div>
            <button className="secondary" onClick={() => setModal(null)}>
              <X size={18} /> Beenden
            </button>
          </div>
          {testStep < 5 ? (
            <button
              className="test-target"
              style={{
                left: targets[testStep][0] * 100 + "%",
                top: targets[testStep][1] * 100 + "%",
              }}
              aria-label={`Ziel ${testStep + 1}`}
              onClick={() => {
                if (!state.native)
                  handleShot(
                    targets[testStep][0],
                    targets[testStep][1],
                    modal.player,
                  );
              }}
            >
              <Crosshair size={65} />
              <span>{testStep + 1}</span>
            </button>
          ) : (
            <div className="test-complete">
              <Check size={65} />
              <h2>
                {state.native
                  ? "Eingabetest abgeschlossen."
                  : "Bedienprobe abgeschlossen."}
              </h2>
              <p>
                Größte Abweichung der angenommenen Treffer:{" "}
                {Math.round(Math.max(...testErrors, 0))} px
                <br />
                {testMisses} Schüsse außerhalb des Zielbereichs.
              </p>
              <p>
                Dieser Test bestätigt keine Firmwarekalibrierung und misst keine
                Eingabelatenz.
              </p>
              <button className="primary" onClick={() => setModal(null)}>
                Zurück zu meinen Guns
              </button>
            </div>
          )}
          <div className="target-progress" aria-live="polite">
            {testStep} / 5 Ziele · {testMisses} Fehlschüsse
          </div>
        </div>
      )}
      {session && (
        <div className="session-banner">
          <Gamepad2 size={20} />
          <span>Ein Spiel läuft.</span>
          <button className="secondary" onClick={() => send("end-game")}>
            Spiel beenden
          </button>
        </div>
      )}
    </div>
  );
}
createRoot(document.getElementById("root")!).render(<App />);
