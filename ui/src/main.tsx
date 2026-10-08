import {
  t,
  message,
  setLanguage,
  normalizeLanguage,
  getLanguage,
  locale,
} from "./i18n";
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
  type GunSignal,
} from "./types";
import "./style.css";
import { GunStudio, GunHeader } from "./GunStudio";
import { GunScroll } from "./GunScroll";
import { MediaPreview } from "./MediaPreview";
import { FilePicker, ArcadeKeyboard, type PickerState } from "./ArcadeDialogs";
type Page = "play" | "guns" | "import" | "settings";
type Modal =
  | {
      type: "game";
      game: Game;
    }
  | {
      type: "launch";
      game: Game;
    }
  | {
      type: "test";
      player: number;
    }
  | {
      type: "report";
      count: number;
      warnings: string[];
      validation?: boolean;
    }
  | null;
const icons = {
  play: Gamepad2,
  guns: Crosshair,
  import: Library,
  settings: Settings,
};
const nav = (): [Page, string][] => [
  ["play", t("Spielen")],
  ["guns", t("Meine Guns")],
  ["import", t("Spiele finden")],
  ["settings", t("Einstellungen")],
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
  const [state, setState] = useState<State>(() => {
    const language = empty.native
      ? "en"
      : normalizeLanguage(localStorage.getItem("reaper-language"));
    setLanguage(language);
    return { ...empty, settings: { ...empty.settings, language } };
  });
  setLanguage(state.settings.language);
  const [picker, setPicker] = useState<PickerState | null>(null);
  const [keyboard, setKeyboard] = useState<"search" | "cover" | null>(null);
  const [page, setPage] = useState<Page>("play");
  const [demo, setDemo] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const [filter, setFilter] = useState("all");
  const [platform, setPlatform] = useState("all");
  const [previewEnabled, setPreviewEnabled] = useState(true);
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
  const [gunSignal, setGunSignal] = useState<GunSignal | null>(null);
  const [lastInput, setLastInput] = useState<Input | null>(null);
  const [testStep, setTestStep] = useState(0);
  const [testErrors, setTestErrors] = useState<number[]>([]);
  const [testMisses, setTestMisses] = useState(0);
  const [testResults, setTestResults] = useState<
    Record<
      number,
      {
        max: number;
        at: string;
      }
    >
  >({});
  const [justEnded, setJustEnded] = useState(false);
  const heroRef = useRef<HTMLElement>(null);
  const stateRef = useRef(state);
  const buttonTestRef = useRef(false);
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
    if (type === "button-test") {
      buttonTestRef.current = typeof (payload as {player?:unknown}).player === "number";
      if (!window.chrome?.webview) return;
    }
    if (type === "set-language") {
      const language = normalizeLanguage(
        (payload as { language?: unknown }).language,
      );
      setLanguage(language);
      setState((s) => ({ ...s, settings: { ...s.settings, language } }));
      if (!window.chrome?.webview) {
        localStorage.setItem("reaper-language", language);
        return;
      }
    }
    if (window.chrome?.webview)
      window.chrome.webview.postMessage({ type, payload });
    else
      announce(
        t(
          "Diese Funktion arbeitet in der Windows-App mit deinem PC. Hier siehst du die Bedienvorschau.",
        ),
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
          at: new Date().toLocaleTimeString(locale(), {
            hour: "2-digit",
            minute: "2-digit",
          }),
        },
      }));
  };
  useEffect(() => {
    const bridge = window.chrome?.webview;
    if (!bridge) return;
    const receive = (event: {
      data: {
        type: string;
        payload: any;
      };
    }) => {
      const { type, payload } = event.data;
      if (type === "picker") setPicker(payload.closed ? null : payload);
      if (type === "state") {
        setLanguage(payload.settings?.language);
        setState({
          ...payload,
          settings: {
            ...payload.settings,
            language: normalizeLanguage(payload.settings?.language),
          },
        });
        setDemo(false);
      }
      if (type === "error" || type === "notice") announce(payload.message);
      if (type === "busy") setBusy(payload.message);
      if (type === "dependency-blocked") {
        setModal(null);
        setPage("settings");
        announce(payload.message);
      }
      if (type === "import-result") {
        setModal({
          type: "report",
          count: payload.count,
          warnings: payload.warnings,
          validation: payload.validation === true,
        });
        setPage("play");
      }
      if (type === "session") {
        setSession(payload.status === "running");
        if (payload.status === "ended") setJustEnded(true);
      }
      if (type === "gun-input") setGunSignal(payload);
      if (type === "button-test-ended") {
        buttonTestRef.current = false;
        window.dispatchEvent(new Event("deadeye-button-test-ended"));
      }
      if (type === "menu-action") {
        if (buttonTestRef.current) return;
        const keys: Record<string, string> = {
          start: "Enter",
          coin: "Escape",
          up: "ArrowUp",
          down: "ArrowDown",
          left: "ArrowLeft",
          right: "ArrowRight",
          secondary: "Escape",
        };
        if (keys[payload.action])
          document.dispatchEvent(
            new KeyboardEvent("keydown", {
              key: keys[payload.action],
              bubbles: true,
            }),
          );
      }
      if (type === "input") {
        if (stateRef.current.remoteSession) return;
        const input = payload as Input;
        setLastInput(input);
        if (buttonTestRef.current && input.player > 0) {
          if (input.kind === "mouse" && input.x !== undefined && input.y !== undefined)
            setAim({x:input.x,y:input.y,player:input.player});
          return;
        }
        if (input.kind === "keyboard" && input.down) {
          const keys: Record<string, string> = {
            start: "Enter",
            coin: "Escape",
            up: "ArrowUp",
            down: "ArrowDown",
            left: "ArrowLeft",
            right: "ArrowRight",
            shoot: "Enter",
            reload: "Escape",
          };
          const action = (
            payload as {
              action?: string;
            }
          ).action;
          if (action && keys[action])
            document.dispatchEvent(
              new KeyboardEvent("keydown", {
                key: keys[action],
                bubbles: true,
              }),
            );
        }
        if (
          input.kind === "mouse" &&
          input.x !== undefined &&
          input.y !== undefined
        ) {
          if (input.player > 0)
            setAim({ x: input.x, y: input.y, player: input.player });
          if ((input.buttons ?? 0) & 1) {
            const hit = document.elementFromPoint(
              input.x * window.innerWidth,
              input.y * window.innerHeight,
            );
            const button = hit?.closest("button") as HTMLButtonElement | null;
            if (
              modalRef.current?.type === "test" &&
              stepRef.current < 5 &&
              (!button || button.classList.contains("test-target"))
            )
              handleShot(input.x, input.y, input.player);
            else if (button && !button.disabled && !button.closest("[inert]"))
              button.click();
            else if (
              hit instanceof HTMLInputElement ||
              hit instanceof HTMLSelectElement
            )
              hit.focus();
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
      if (event.isTrusted && !stateRef.current.remoteSession) {
        event.preventDefault();
        event.stopImmediatePropagation();
      }
    };
    document.addEventListener("click", legacy, true);
    const testLegacy = (event: Event) => {
      if (buttonTestRef.current && !stateRef.current.remoteSession) {
        event.preventDefault(); event.stopImmediatePropagation();
      }
    };
    for (const name of ["contextmenu", "auxclick", "keydown", "keyup"])
      document.addEventListener(name, testLegacy, true);
    return () => {
      bridge.removeEventListener("message", receive);
      document.removeEventListener("click", legacy, true);
      for (const name of ["contextmenu", "auxclick", "keydown", "keyup"])
        document.removeEventListener(name, testLegacy, true);
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
      if (event.isTrusted && state.native && !state.remoteSession) {
        event.preventDefault();
        return;
      }
      let k = event.key;
      if (state.native) k = map[k.toLowerCase()] ?? k;
      if (k === "Escape" || (state.native && k === "5")) {
        event.preventDefault();
        if (picker) send("cancel-picker");
        else if (keyboard) setKeyboard(null);
        else if (state.bindingStage) send("cancel-bind");
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
  }, [state.native, state.bindingStage, modal, page, picker, keyboard]);
  const allGames = demo ? examples : state.games;
  const games = allGames.filter(
    (g) =>
      (filter !== "favorites" || g.favorite) &&
      (filter !== "modern" ||
        g.source === "teknoparrot" ||
        g.platform === "TeknoParrot") &&
      (filter !== "priority" || g.priority === 1) &&
      (filter !== "available" || g.status !== "needs-setup") &&
      (platform === "all" || g.platform === platform) &&
      (filter !== "classic" || g.source === "mame" || g.platform === "MAME") &&
      g.title.toLowerCase().includes(search.toLowerCase()),
  );
  const selectGame = (id:string) => {
    setSelected(id);
    requestAnimationFrame(()=>{
      window.scrollTo({top:0,behavior:"instant"});
      heroRef.current?.querySelector<HTMLButtonElement>(".primary")?.focus({preventScroll:true});
    });
  };
  const featured =
    allGames.find((g) => g.id === selected) ?? games[0] ?? allGames[0];
  const connected = (player: number) => {
    const binding = state.bindings.find((b) => b.player === player);
    return (
      binding &&
      state.devices.some(
        (d) => d.id.toLowerCase() === binding.mouseId.toLowerCase(),
      )
    );
  };
  const connectedCount = state.bindings.filter((b) =>
    connected(b.player),
  ).length;
  const start = (g: Game) => {
    if (g.source === "demo") {
      announce(
        t(
          "Beispielspiel \u2014 importiere deine Installation, um es wirklich zu starten.",
        ),
      );
      return;
    }
    if (g.status === "needs-setup") {
      announce(
        t(
          "Die Einrichtung ist noch unvollst\u00E4ndig. In den Spieldetails findest du die fehlenden Dateien; danach Bibliothek erneut pr\u00FCfen.",
        ),
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
      <GunScroll hidden={!!modal||!!state.bindingStage||!!picker||!!keyboard||!!state.learning||session}/>
      <aside
        className="sidebar"
        inert={!!modal || !!state.bindingStage || !!picker || !!keyboard}
      >
        <button
          className="brand"
          onClick={() => setPage("play")}
          aria-label={t("Deadeye Arcade Start")}
        >
          <Crosshair size={30} />
          <span>
            {t("DEADEYE")}
            <small>{t("ARCADE")}</small>
          </span>
        </button>
        <div className="nav-label">{t("DEINE ARCADE")}</div>
        <nav>
          {nav().map(([id, title]) => {
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
            {state.native ? t("Windows verbunden") : t("Bedienvorschau")}
          </div>
          <span>
            {t("Deadeye Arcade \u00B7") + " "}
            {state.version}
          </span>
        </div>
      </aside>
      {!state.native && (
        <button className="persistent-exit" onClick={() => send("close")}>
          <Power size={20} />
          {" " + t("App schlie\u00DFen \u00B7 Windows")}
        </button>
      )}
      <div
        className="main-shell"
        inert={!!modal || !!state.bindingStage || !!picker || !!keyboard}
      >
        <header className="topbar">
          <div className="breadcrumb">
            {t("DEIN SPIELZIMMER") + " "}
            <span>/</span>{" "}
            {nav()
              .find((n) => n[0] === page)?.[1]
              .toUpperCase()}
          </div>
          <div className="top-status">
            <GunHeader state={state} open={() => setPage("guns")} />
            <span className="version-chip">{t("EARLY ACCESS")}</span>
          </div>
        </header>
        {!state.native && (
          <div className="preview-strip">
            <Info size={16} />
            <span>
              {t(
                "Bedienvorschau im Browser. Ger\u00E4te, Dateiimport und Spielstart arbeiten in der Windows-App.",
              )}
            </span>
          </div>
        )}
        {demo && (
          <div className="preview-strip demo-strip">
            <ImageIcon size={16} />
            <span>
              {t(
                "Beispielbibliothek \u00B7 diese Spiele sind hier nicht installiert",
              )}
            </span>
            <button
              onClick={() => {
                setDemo(false);
                setSelected(null);
              }}
            >
              {t("Vorschau verlassen") + " "}
              <X size={14} />
            </button>
          </div>
        )}
        {state.remoteSession && (
          <div className="preview-strip">
            <Monitor size={18} />
            <span>
              {t(
                "Remote Desktop aktiv \u00B7 Men\u00FCs mit Maus testen. Gun-Zuordnung und Kalibrierung am echten Bildschirm pr\u00FCfen.",
              )}
            </span>
          </div>
        )}
        <main>
          {state.update?.status === "available" && page !== "settings" && <div className="remote-banner"><Download size={20}/><span>{t("Neue Version verfügbar.")} {state.update.release?.version}</span><button className="secondary" onClick={()=>setPage("settings")}>{t("App-Updates")}</button></div>}
          {page === "play" && (
            <>
              {featured ? (
                <>
                  <section
                    className="hero"
                    ref={heroRef}
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
                        {featured.description || (featured.platform === "MAME"
                          ? t("Die Klassiker. Dein Ziel. Deine Arcade.")
                          : t(
                              "Gro\u00DFe Arcade-Action. Direkt in deinem Spielzimmer.",
                            ))}
                      </p>
                      <div className="hero-meta">
                        {featured.releaseYear && <span>{featured.releaseYear}</span>}
                        {featured.releaseInfo && <span>{featured.releaseInfo}</span>}
                        {featured.hardware && <span className="hardware-tag" title={t("Original-Hardware")}>{featured.hardware}</span>}
                        <span>
                          <Monitor size={15} /> {featured.aspect}
                        </span>
                        <span>
                          <Crosshair size={15} />
                          {" " + t("Lightgun-Profil")}
                        </span>
                      </div>
                      <div className="hero-buttons">
                        <button
                          className="primary"
                          onClick={() => start(featured)}
                          disabled={!!busy || featured.status === "needs-setup"}
                        >
                          <Play size={20} fill="currentColor" />
                          {featured.source === "demo"
                            ? t("Startablauf ansehen")
                            : t("Spiel starten")}
                        </button>
                        <button
                          className="secondary"
                          onClick={() =>
                            setModal({ type: "game", game: featured })
                          }
                        >
                          {t("Spieldetails") + " "}
                          <ArrowRight size={17} />
                        </button>
                        {featured.previewVideo && (
                          <button
                            className="secondary"
                            onClick={() => setPreviewEnabled(!previewEnabled)}
                          >
                            {previewEnabled
                              ? t("Vorschau pausieren")
                              : t("Vorschau abspielen")}
                          </button>
                        )}
                      </div>
                      {featured.players && (
                        <p className="players-note">
                          {featured.players}
                          {" " +
                            t("\u00B7 Gun-Einrichtung separat pr\u00FCfen")}
                        </p>
                      )}
                    </div>
                    <div className="hero-art">
                      {featured.cover ||
                      featured.previewVideo ||
                      featured.screenshot ||
                      featured.logo ? (
                        <MediaPreview
                          key={featured.id + featured.previewVideo}
                          game={featured}
                          active={previewEnabled && !modal && !session && !busy}
                        />
                      ) : (
                        <>
                          <div className="art-grid" />
                          <div className="orbit orbit-one" />
                          <div className="orbit orbit-two" />
                          <div className="art-cross" />
                          <div className="art-number">
                            {featured.title.includes(t("Time Crisis"))
                              ? "05"
                              : featured.title.includes("Jurassic")
                                ? "JP"
                                : featured.title.includes("Point")
                                  ? "PB"
                                  : "RS"}
                          </div>
                          <div className="art-caption">
                            {t("AIM FOR SOMETHING GREAT")}
                          </div>
                        </>
                      )}
                    </div>
                  </section>
                  <div className="library-toolbar">
                    <div>
                      <h2>
                        {t("Deine Spiele") + " "}
                        <span>{games.length}</span>
                      </h2>
                      <p>
                        {demo
                          ? t("So k\u00F6nnte deine Sammlung aussehen.")
                          : t(
                              "Importiert. An einem Ort. Bereit f\u00FCr den n\u00E4chsten Spieltest.",
                            )}
                      </p>
                    </div>
                    <div className="library-tools">
                      <label className="search-box">
                        <Search size={16} />
                        <input
                          aria-label={t("Spiele suchen")}
                          placeholder={t("Spiel suchen")}
                          value={search}
                          onChange={(e) => setSearch(e.target.value)}
                          onFocus={() => {
                            if (state.native && !state.remoteSession) setKeyboard("search");
                          }}
                        />
                      </label>
                      <button
                        className="icon-button"
                        onClick={() => setPage("import")}
                        aria-label={t("Spiele importieren")}
                      >
                        <Plus size={21} />
                      </button>
                    </div>
                  </div>
                  <label className="platform-filter">
                    {t("System ausw\u00E4hlen") + " "}
                    <select
                      aria-label={t("System filtern")}
                      value={platform}
                      onChange={(e) => {
                        setPlatform(e.target.value);
                        setSelected(null);
                      }}
                    >
                      {[
                        "all",
                        ...Array.from(
                          new Set(allGames.map((g) => g.platform)),
                        ).sort(),
                      ].map((p) => (
                        <option key={p} value={p}>
                          {p === "all" ? t("Alle Systeme") : p}
                        </option>
                      ))}
                    </select>
                  </label>
                  <div className="filters">
                    {[
                      ["all", t("Alle Spiele")],
                      ["modern", t("Modern Arcade")],
                      ["classic", t("Klassiker")],
                      ["favorites", t("Favoriten")],
                      ["priority", t("Erste Auswahl")],
                      ["available", t("Dateien vorhanden")],
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
                        aria-label={game.title + t(" ausw\u00E4hlen")}
                        onClick={() => selectGame(game.id)}
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
                          <span>{game.releaseYear ? `${game.releaseYear} · ` : ""}{game.platform}</span>
                        </div>
                      </button>
                    ))}
                  </div>
                  {games.length === 0 && (
                    <div className="no-results">
                      {t("Keine Spiele in dieser Auswahl.")}
                    </div>
                  )}
                </>
              ) : (
                <section className="welcome">
                  <div className="welcome-art">
                    <div className="orbit" />
                    <Crosshair size={105} />
                    <span>
                      {t("YOUR ARCADE")}
                      <br />
                      {t("STARTS HERE.")}
                    </span>
                  </div>
                  <div className="welcome-content">
                    <div className="eyebrow">
                      {t("WILLKOMMEN IN DEINER ARCADE")}
                    </div>
                    <h1>
                      {t("Guns nehmen.")}
                      <br />
                      {t("Spiele starten.")}
                    </h1>
                    <p>
                      {t(
                        "Wir verbinden deine Lightguns und bringen deine Spiele in eine gemeinsame Bibliothek.",
                      )}
                    </p>
                    <div className="welcome-steps">
                      <span>
                        <b>01</b>
                        {" " + t("Guns zuordnen")}
                      </span>
                      <span>
                        <b>02</b>
                        {" " + t("Spiele importieren")}
                      </span>
                      <span>
                        <b>03</b>
                        {" " + t("Loslegen")}
                      </span>
                    </div>
                    <div className="hero-buttons">
                      <button
                        className="primary"
                        onClick={() => setPage("guns")}
                      >
                        <Crosshair size={19} />
                        {" " + t("Guns einrichten")}
                      </button>
                      <button
                        className="secondary"
                        onClick={() => setPage("import")}
                      >
                        <Plus size={18} />
                        {" " + t("Spiele hinzuf\u00FCgen")}
                      </button>
                    </div>
                    <button
                      className="text-button"
                      onClick={() => {
                        setDemo(true);
                        setSelected(null);
                      }}
                    >
                      {t("Mit Beispielspielen ansehen") + " "}
                      <ArrowRight size={15} />
                    </button>
                  </div>
                </section>
              )}
              {justEnded && (
                <div className="after-game">
                  <Check size={18} />
                  <span>
                    {t(
                      "Zur\u00FCck in deiner Arcade. Wenn alles funktioniert hat, kannst du das Spiel in den Details als spielbar best\u00E4tigen.",
                    )}
                  </span>
                  <button
                    onClick={() => setJustEnded(false)}
                    aria-label={t("Hinweis schlie\u00DFen")}
                  >
                    <X size={16} />
                  </button>
                </div>
              )}
            </>
          )}
          {page === "guns" && (
            <GunStudio
              state={state}
              signal={gunSignal}
              send={send}
              test={test}
              busy={!!busy}
              results={testResults}
            />
          )}
          {page === "import" && (
            <>
              <div className="page-heading">
                <div className="eyebrow">{t("EINE GEMEINSAME BIBLIOTHEK")}</div>
                <h1>
                  {t("Deine Spiele.")}
                  <br />
                  {t("Ein Startpunkt.")}
                </h1>
                <p>
                  {t(
                    "W\u00E4hle deine vorhandenen Installationen. Spieldateien bleiben an ihrem bisherigen Ort.",
                  )}
                </p>
              </div>
              <div className="inline-actions">
                <button
                  className="primary"
                  disabled={!!busy}
                  onClick={() => send("scan-installations")}
                >
                  <Search size={20} />
                  {t("Installationen automatisch finden")}
                </button>
                <button
                  className="secondary"
                  onClick={() => send("validate-library")}
                >
                  <ShieldCheck size={18} />
                  {t("Bibliothek pr\u00FCfen")}
                </button>
                <button
                  className="secondary"
                  disabled={!!busy}
                  onClick={() => send("import-collection")}
                >
                  <FolderOpen size={18} />
                  {" " + t("\u00DCbergabepaket importieren")}
                </button>
              </div>
              {state.installations?.length > 0 && (
                <div className="detected-list">
                  {state.installations.map((i) => (
                    <div key={i.path}>
                      <div>
                        <strong>{i.name}</strong>
                        <small>{i.path}</small>
                      </div>
                      {i.kind === "tekno" || i.kind === "mame" ? (
                        <button
                          className="secondary"
                          onClick={() =>
                            send(
                              i.kind === "tekno"
                                ? "import-tekno"
                                : "import-mame",
                              { path: i.path },
                            )
                          }
                        >
                          {t("Spiele importieren")}
                        </button>
                      ) : (
                        <span>
                          {t("Programm gefunden \u00B7 Spiele-Anbindung folgt")}
                        </span>
                      )}
                    </div>
                  ))}
                </div>
              )}
              <div className="import-grid">
                {action(
                  "TeknoParrot",
                  t("Vorhandene Lightgun-Spielprofile automatisch einlesen"),
                  <Crosshair size={30} />,
                  () => send("import-tekno"),
                )}
                {action(
                  "MAME",
                  t(
                    "Lightgun-Titel aus dem Emulator-Katalog und deinen ROMs erkennen",
                  ),
                  <Gamepad2 size={30} />,
                  () => send("import-mame"),
                )}
                {action(
                  t("Windows-Spiel"),
                  t(
                    "Eine vorhandene Spielanwendung zur Bibliothek hinzuf\u00FCgen",
                  ),
                  <Monitor size={30} />,
                  () => send("add-pc"),
                )}
                {action(
                  t("Cover erg\u00E4nzen"),
                  state.settings.hasCoverKey
                    ? t("Eindeutige Cover automatisch laden")
                    : t(
                        "Eigene Bilder verwenden oder API-Schl\u00FCssel hinterlegen",
                      ),
                  <ImageIcon size={30} />,
                  () =>
                    state.settings.hasCoverKey
                      ? send("fetch-covers")
                      : setPage("settings"),
                )}
              </div>
              <div className="import-explainer">
                <span className="eyebrow">{t("WAS BEIM IMPORT PASSIERT")}</span>
                <div className="import-flow">
                  {[
                    t("Spiele finden"),
                    t("Profil zuordnen"),
                    t("Cover erg\u00E4nzen"),
                    t("Im Spiel pr\u00FCfen"),
                  ].map((title, i) => (
                    <div key={title}>
                      <b>0{i + 1}</b>
                      <strong>{title}</strong>
                      {i < 3 && <ArrowRight size={16} />}
                    </div>
                  ))}
                </div>
                <p>
                  {t(
                    "TeknoParrot nutzt bereits angelegte UserProfiles. MAME liefert seine Lightgun-Liste selbst. Fehlende ROM-Bestandteile und Spieleinstellungen werden erst beim tats\u00E4chlichen Spieltest sichtbar.",
                  )}
                </p>
              </div>
              <div className="coming-next">
                <strong>{t("N\u00E4chste Anbindungen")}</strong>
                <span>
                  {t(
                    "DuckStation \u00B7 PCSX2 \u00B7 Dolphin \u00B7 Flycast \u00B7 Model 2 \u00B7 Supermodel \u00B7 Steam",
                  )}
                </span>
                <p>
                  {t(
                    "Eigene Importadapter für diese Systeme sind noch nicht implementiert. Vorhandene Startwege können über eine Sammlungsübergabe importiert werden.",
                  )}
                </p>
              </div>
            </>
          )}
          {page === "settings" && (
            <>
              <div className="page-heading">
                <div className="eyebrow">{t("DEIN ARCADE-PC")}</div>
                <h1>
                  {t("Einmal einstellen.")}
                  <br />
                  {t("Entspannt spielen.")}
                </h1>
              </div>
              <section className="settings-panel">
                <div className="setting-block update-panel">
                  <div className="setting-title"><Download size={20}/><strong>{t("App-Updates")}</strong></div>
                  <p>{t("Installierte Version")}: {state.version} · {t("Reguläre GitHub-Releases")}</p>
                  <div className="setting-row">
                    <div><strong>{t("Automatisch nach Updates suchen")}</strong><p>{t("Beim Start und alle sechs Stunden. Während eines Spiels wird nichts installiert.")}</p></div>
                    <button className={"toggle " + (state.settings.checkForUpdates !== false ? "on" : "")} role="switch" aria-checked={state.settings.checkForUpdates !== false} aria-label={t("Automatisch nach Updates suchen")} onClick={()=>send("update-preference",{enabled:state.settings.checkForUpdates===false})}><span/></button>
                  </div>
                  <p role="status">{t(({idle:"Noch nicht geprüft.",checking:"Update wird gesucht …",current:"Kein neueres Installationsrelease verfügbar.",available:"Neue Version verfügbar.",downloading:"Update wird heruntergeladen …",installing:"Update wird installiert …",error:"Update konnte nicht abgeschlossen werden."} as Record<string,string>)[state.update?.status??"idle"]??"Noch nicht geprüft.")}
                  {state.update?.release && <> · {state.update.release.version}</>}
                  {state.update?.status==="downloading" && <> · {state.update.progress}%</>}</p>
                  {state.update?.error && <p className="hint">{message(state.update.error)}</p>}
                  <div className="button-row">
                    <button className="secondary" disabled={session || !!busy || ["checking","downloading","installing"].includes(state.update?.status??"")} onClick={()=>send("check-updates")}><RefreshCw size={20}/>{t("Nach Updates suchen")}</button>
                    <button className="primary" disabled={session || !!busy || !state.update?.release || ["checking","downloading","installing"].includes(state.update?.status??"")} onClick={()=>send("install-update")}><Download size={20}/>{t("Update installieren und neu starten")}</button>
                  </div>
                  <p className="hint">{t("Das Update wird geprüft. Bibliothek, Gun-Belegung und Einstellungen bleiben erhalten. Die App schließt kurz und startet danach wieder.")}</p>
                  {state.update?.release?.notes && <details><summary>{t("Änderungen in dieser Version")}</summary><p style={{whiteSpace:"pre-wrap"}}>{state.update.release.notes}</p></details>}
                </div>
                <div className="setting-row language-setting">
                  <div>
                    <strong>{t("Sprache")}</strong>
                    <p>{t("Für Oberfläche, Spielmenü und App-Meldungen.")}</p>
                  </div>
                  <div
                    className="language-buttons"
                    role="group"
                    aria-label={t("Sprache")}
                  >
                    <button
                      className={
                        state.settings.language === "en"
                          ? "primary"
                          : "secondary"
                      }
                      aria-pressed={state.settings.language === "en"}
                      onClick={() => send("set-language", { language: "en" })}
                    >
                      English
                    </button>
                    <button
                      className={
                        state.settings.language === "de"
                          ? "primary"
                          : "secondary"
                      }
                      aria-pressed={state.settings.language === "de"}
                      onClick={() => send("set-language", { language: "de" })}
                    >
                      Deutsch
                    </button>
                  </div>
                </div>
                <div className="setting-row">
                  <div>
                    <strong>{t("Direkt im Vollbild starten")}</strong>
                    <p>
                      {t(
                        "Gro\u00DFe Oberfl\u00E4che f\u00FCr deinen Bildschirm.",
                      )}
                    </p>
                  </div>
                  <button
                    className={
                      "toggle " + (state.settings.fullscreen ? "on" : "")
                    }
                    role="switch"
                    aria-checked={state.settings.fullscreen}
                    aria-label={t("Vollbild")}
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
                    <strong>{t("Mit Windows starten")}</strong>
                    <p>
                      {t("\u00D6ffnet die App nach deiner Windows-Anmeldung.")}
                    </p>
                  </div>
                  <button
                    className={
                      "toggle " + (state.settings.startWithWindows ? "on" : "")
                    }
                    role="switch"
                    aria-checked={state.settings.startWithWindows}
                    aria-label={t("Autostart mit Windows")}
                    onClick={() =>
                      send("autostart", {
                        enabled: !state.settings.startWithWindows,
                      })
                    }
                  >
                    <span />
                  </button>
                </div>
                <div className="setting-row">
                  <div>
                    <strong>{t("P1 / P2 Fadenkreuze auf dem Desktop")}</strong>
                    <p>{t("Blaue, beschriftete Ziele für zugeordnete Guns. Läuft auch bei geschlossener App; verschwindet in Spielen und fängt keine Klicks ab.")}</p>
                  </div>
                  <button className={"toggle " + (state.settings.desktopCrosshairs !== false ? "on" : "")} role="switch" aria-checked={state.settings.desktopCrosshairs !== false} aria-label={t("Desktop-Fadenkreuze")} onClick={() => send("desktop-crosshairs", {enabled: state.settings.desktopCrosshairs === false})}><span /></button>
                </div>
                <div className="setting-block">
                  <div className="setting-title">
                    <ShieldCheck size={20} />
                    <strong>
                      {t("Spiele und Emulatoren: ben\u00F6tigte Laufzeiten")}
                    </strong>
                  </div>
                  <p>
                    {t(
                      "Deadeye pr\u00FCft automatisch beim \u00D6ffnen, nach dem Import und vor dem Spielstart. Fehlende Visual-C++-, DirectX- und .NET-8/9/10-Laufzeiten werden passenden Microsoft-Paketen zugeordnet.",
                    )}
                  </p>
                  <div className="inline-actions">
                    <button
                      className="secondary"
                      disabled={!!busy}
                      onClick={() => send("check-dependencies")}
                    >
                      <RefreshCw size={18} />
                      {" " + t("Jetzt pr\u00FCfen")}
                    </button>
                    <button
                      className="primary"
                      disabled={
                        !!busy ||
                        !state.dependencies?.packages.some((p) => p.missing)
                      }
                      onClick={() => send("install-dependencies")}
                    >
                      <Download size={18} />
                      {" " + t("Fehlende Pakete installieren")}
                    </button>
                  </div>
                  <p>
                    {t(
                      "Download und Signaturpr\u00FCfung erfolgen automatisch. Im Microsoft-Installer best\u00E4tigst du die Lizenz und gegebenenfalls die Windows-Abfrage. Anschlie\u00DFend pr\u00FCft Deadeye erneut.",
                    )}
                  </p>
                  {state.dependencies && (
                    <>
                      <p>
                        {state.dependencies.checkedBinaries}
                        {" " +
                          t(
                            "Programme und lokale Bibliotheken gepr\u00FCft \u00B7 Stand",
                          ) +
                          " "}
                        {new Date(state.dependencies.time).toLocaleTimeString(
                          locale(),
                        )}
                      </p>
                      {state.dependencies.packages.map((p) => (
                        <div className="setting-row" key={p.id}>
                          <div>
                            <strong>{message(p.name)}</strong>
                            <p>
                              {p.games.slice(0, 4).join(" · ")}
                              {p.games.length > 4
                                ? t(
                                    " \u00B7 und {0} weitere",
                                    p.games.length - 4,
                                  )
                                : ""}
                            </p>
                            {p.missing && (
                              <p>
                                {t("Fehlt:") + " "}
                                {p.dlls.join(", ")}
                              </p>
                            )}
                          </div>
                          <span
                            className={
                              "connection-tag " + (p.missing ? "" : "online")
                            }
                          >
                            {p.missing
                              ? t("Installation n\u00F6tig")
                              : t("Dateien vorhanden")}
                          </span>
                        </div>
                      ))}
                      <p>
                        {t(
                          "Diese Pr\u00FCfung erkennt bekannte Laufzeit-Dateien. Dynamisch geladene Komponenten, Treiber und einzelne Spielprofile ben\u00F6tigen zus\u00E4tzlich einen Spieltest.",
                        )}
                      </p>
                      {state.dependencies.uncheckedCount > 0 && (
                        <details>
                          <summary>
                            {state.dependencies.uncheckedCount}
                            {" " + t("offene Dateizuordnungen anzeigen")}
                          </summary>
                          <ul>
                            {state.dependencies.uncheckedFiles.map(
                              (entry, i) => (
                                <li key={i}>{message(entry)}</li>
                              ),
                            )}
                          </ul>
                        </details>
                      )}
                    </>
                  )}
                </div>
                <div className="setting-block">
                  <div className="setting-title">
                    <ImageIcon size={20} />
                    <strong>{t("Automatische Spielecover")}</strong>
                    {state.settings.hasCoverKey && (
                      <span className="connection-tag online">
                        {t("Schl\u00FCssel gespeichert")}
                      </span>
                    )}
                  </div>
                  <p>
                    {t(
                      "Optionaler SteamGridDB-API-Schl\u00FCssel. Er wird unter deinem Windows-Benutzer verschl\u00FCsselt gespeichert. Alternativ kannst du jedem Spiel ein lokales Cover geben.",
                    )}
                  </p>
                  <label className="key-entry">
                    <input
                      type="password"
                      autoComplete="off"
                      placeholder={t("SteamGridDB-API-Schl\u00FCssel")}
                      aria-label={t("SteamGridDB API Schl\u00FCssel")}
                      value={coverKey}
                      onChange={(e) => setCoverKey(e.target.value)}
                      onFocus={() => {
                        if (state.native) setKeyboard("cover");
                      }}
                    />
                    <button
                      className="secondary"
                      onClick={() => {
                        send("set-cover-key", { key: coverKey });
                        setCoverKey("");
                      }}
                    >
                      {t("Speichern")}
                    </button>
                  </label>
                  <div className="inline-actions">
                    <button
                      className="primary"
                      onClick={() => send("fetch-covers")}
                      disabled={!state.settings.hasCoverKey}
                    >
                      <Download size={17} />
                      {" " + t("Fehlende Cover laden")}
                    </button>
                    {state.settings.hasCoverKey && (
                      <button
                        className="text-button"
                        onClick={() => send("set-cover-key", { key: "" })}
                      >
                        {t("Schl\u00FCssel entfernen")}
                      </button>
                    )}
                  </div>
                </div>
                <div className="setting-block">
                  <div className="setting-title">
                    <Info size={20} />
                    <strong>
                      {t("Diagnose f\u00FCr den ersten Windows-Test")}
                    </strong>
                  </div>
                  <p>
                    {t(
                      "Speichert Ger\u00E4tekennungen, Zuordnungen und Spielezust\u00E4nde. API-Schl\u00FCssel werden nicht exportiert.",
                    )}
                  </p>
                  <button
                    className="secondary"
                    onClick={() => send("export-diagnostics")}
                  >
                    <Download size={17} />
                    {" " + t("Diagnose speichern")}
                  </button>
                </div>
              </section>
              <div className="setup-note">
                <Keyboard size={22} />
                <div>
                  <strong>
                    {t("Mit der Gun zur\u00FCck zum Men\u00FC oder zu Windows")}
                  </strong>
                  <p>
                    {t(
                      "Auf der zugeordneten Gun Start + M\u00FCnze f\u00FCr etwa zwei Sekunden halten. Spieler 1: Tasten 1 + 5, Spieler 2: 2 + 6. Im Spiel f\u00FChrt das zur\u00FCck ins Men\u00FC. Abzug mindestens 10 Sekunden halten \u00F6ffnet das Spielmen\u00FC mit Neustart, Beenden und Tasten\u00FCbersicht. Danach den Abzug loslassen. Dort beide Start-/M\u00FCnztasten loslassen und erneut halten, um die App zu schlie\u00DFen. Der Knopf \u201EApp schlie\u00DFen \u00B7 Windows\u201C bleibt auch bei Dialogen und laufenden Pr\u00FCfungen sichtbar. Mit einer Tastatur: F10 Spielmen\u00FC, F12 Spiel beenden.",
                    )}
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
            {t("Zielen & Abzug: ausw\u00E4hlen")}
          </span>
          <span>
            <b>↔</b>
            {" " + t("Stick: navigieren")}
          </span>
          <span>
            <b>↩</b>
            {" " + t("Zur\u00FCck: eine Ebene zur\u00FCck")}
          </span>
          <span className="footer-right">{t("MADE FOR YOUR GAME ROOM")}</span>
        </footer>
      </div>
      {busy && (
        <div className="busy-bar" role="status">
          <RefreshCw size={17} />
          {message(busy)}
        </div>
      )}
      {toast && (
        <div className="toast" role="status">
          <Info size={19} />
          <span>{message(toast)}</span>
          <button
            onClick={() => setToast("")}
            aria-label={t("Hinweis schlie\u00DFen")}
          >
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
          <span>
            {t("P")}
            {aim.player}
          </span>
        </div>
      )}
      {state.bindingStage && (
        <div className="modal-backdrop">
          <section
            className="modal bind-modal"
            role="dialog"
            aria-modal="true"
            aria-label={t("Gun zuordnen")}
          >
            <span className="eyebrow">
              {t("SPIELER") + " "}
              {state.bindingStage.player}
              {" " + t("ZUORDNEN")}
            </span>
            <Crosshair className="bind-icon" size={66} />
            <h2>
              {state.bindingStage.stage === "trigger"
                ? t("Dr\u00FCcke den Abzug.")
                : t("Dr\u00FCcke jetzt Start.")}
            </h2>
            <p>
              {state.bindingStage.stage === "trigger"
                ? t(
                    "Nimm die Gun f\u00FCr diesen Spieler und schie\u00DFe einmal. So erkennen wir ihren eigenen Zieleingang.",
                  )
                : t(
                    "Dr\u00FCcke die Start-Taste an derselben Gun. Damit ordnen wir auch ihren Tasteneingang zu.",
                  )}
            </p>
            <div className="wizard-progress">
              <span className="done">{t("1 \u00B7 Zieleingang")}</span>
              <ChevronRight size={16} />
              <span
                className={state.bindingStage.stage === "start" ? "done" : ""}
              >
                {t("2 \u00B7 Tasten")}
              </span>
            </div>
            <button className="secondary" onClick={() => send("cancel-bind")}>
              {t("Abbrechen")}
            </button>
          </section>
        </div>
      )}
      {modal && modal.type !== "test" && (
        <div className="modal-backdrop">
          <section
            className={`modal${modal.type === "launch" ? " launch-modal" : ""}`}
            inert={!!picker || !!keyboard}
            role="dialog"
            aria-modal="true"
            aria-label={
              modal.type === "report" ? t("Importergebnis") : modal.game.title
            }
          >
            <button
              className="modal-close"
              onClick={() => setModal(null)}
              aria-label={t("Dialog schlie\u00DFen")}
            >
              <X size={22} />
            </button>
            {modal.type === "report" ? (
              <>
                <span className="eyebrow">
                  {modal.validation
                    ? t("BIBLIOTHEK GEPR\u00DCFT")
                    : t("IMPORT ABGESCHLOSSEN")}
                </span>
                <h2>
                  {modal.count}
                  {" " + t("Spiele") + " "}
                  {modal.validation
                    ? t("mit vorhandenen Startdateien")
                    : t("eingelesen")}
                  .
                </h2>
                <p>
                  {modal.validation
                    ? t(
                        "Die Startdateien wurden gepr\u00FCft. Zielen und Tasten best\u00E4tigst du nach einem echten Spieltest.",
                      )
                    : t(
                        "Vorhandene Eintr\u00E4ge wurden aktualisiert. Favoriten und eigene Cover bleiben erhalten.",
                      )}
                </p>
                {modal.warnings.length > 0 && (
                  <details>
                    <summary>
                      {modal.warnings.length}
                      {" " + t("Hinweise ansehen")}
                    </summary>
                    <ul className="warning-list">
                      {modal.warnings.map((w, i) => (
                        <li key={i}>{message(w)}</li>
                      ))}
                    </ul>
                  </details>
                )}
                <button className="primary" onClick={() => setModal(null)}>
                  {t("Zur Bibliothek") + " "}
                  <ArrowRight size={18} />
                </button>
              </>
            ) : modal.type === "launch" ? (
              <>
                <div className="launch-information">
                  <span className="eyebrow">
                    {modal.game.platform.toUpperCase()}
                    {" " + t("\u00B7 ERSTER START")}
                  </span>
                  <h2>{modal.game.title}</h2>
                  {modal.game.description && <p className="game-description" lang="en">{modal.game.description}</p>}
                  {(modal.game.releaseYear || modal.game.releaseInfo || modal.game.hardware) && <dl className="game-facts">
                    {(modal.game.releaseYear || modal.game.releaseInfo) && <div><dt>{t("Erscheinungsjahr")}</dt><dd>{[modal.game.releaseYear, modal.game.releaseInfo].filter(Boolean).join(" · ")}</dd></div>}
                    {modal.game.hardware && <div><dt>{t("Original-Hardware")}</dt><dd>{modal.game.hardware}</dd></div>}
                  </dl>}
                  <p>
                    {t(
                      "Die App startet dein gespeichertes Profil. Ob Zielen, Tasten, Pedal und zwei Spieler im Spiel funktionieren, pr\u00FCfen wir auf deinem PC.",
                    )}
                  </p>
                  <div className="launch-checks">
                    <span>
                      <Check size={16} />
                      {" " + t("Vorhandenen Starter verwenden")}
                    </span>
                    <span>
                      <Check size={16} />
                      {" " +
                        t(
                          "RS3-Bildformat setzen, wenn ein COM-Port zugeordnet ist",
                        )}
                    </span>
                    <span>
                      <Check size={16} />
                      {" " + t("Nach Spielende ins Men\u00FC zur\u00FCckkehren")}
                    </span>
                  </div>
                  <div className="exit-reminder">
                    <Keyboard size={19} />
                    <span>
                      {t(
                        "Abzug mindestens 10 Sekunden halten: Spielmen\u00FC mit Neustart, Beenden und Tasten\u00FCbersicht. Danach loslassen. Start + M\u00FCnze etwa 2 Sekunden: direkt beenden. Tastatur: F10 Men\u00FC \u00B7 F12 beenden.",
                      )}
                    </span>
                  </div>
                </div>
                <button
                  className="primary launch-confirm"
                  onClick={() => {
                    send("launch", { id: modal.game.id });
                    setModal(null);
                  }}
                >
                  <Play size={18} />
                  {" " + t("Jetzt starten")}
                </button>
              </>
            ) : (
              <>
                <span className="eyebrow">
                  {modal.game.platform.toUpperCase()} ·{" "}
                  {badge(modal.game).toUpperCase()}
                </span>
                <h2>{modal.game.title}</h2>
                {modal.game.description && <p className="game-description" lang="en">{modal.game.description}</p>}
                {(modal.game.releaseYear || modal.game.releaseInfo || modal.game.hardware) && <dl className="game-facts">
                  {(modal.game.releaseYear || modal.game.releaseInfo) && <div><dt>{t("Erscheinungsjahr")}</dt><dd>{[modal.game.releaseYear, modal.game.releaseInfo].filter(Boolean).join(" · ")}</dd></div>}
                  {modal.game.hardware && <div><dt>{t("Original-Hardware")}</dt><dd>{modal.game.hardware}</dd></div>}
                </dl>}
                <div className="detail-actions">
                  <button
                    className="primary"
                    disabled={modal.game.status === "needs-setup"}
                    onClick={() => start(modal.game)}
                  >
                    <Play size={18} />
                    {" " + t("Spiel starten")}
                  </button>
                  <button
                    className="secondary"
                    onClick={() => send("favorite", { id: modal.game.id })}
                  >
                    <Heart size={17} />
                    {" " + t("Favorit umschalten")}
                  </button>
                </div>
                {(modal.game.setupIssues?.length ?? 0) > 0 && (
                  <div className="setup-issues">
                    <strong>{t("Vor dem Start fehlt noch:")}</strong>
                    <ul>
                      {modal.game.setupIssues?.map((issue) => (
                        <li key={issue}>{message(issue)}</li>
                      ))}
                    </ul>
                  </div>
                )}
                {modal.game.players && (
                  <p>
                    {t("Spieler laut Spiel:") + " "}
                    {modal.game.players}
                    {t(
                      ". Zwei eingerichtete Guns sind noch separat zu pr\u00FCfen.",
                    )}
                  </p>
                )}
                {state.gameCompatibility?.[modal.game.id] && (
                  <div className="setup-warning"><strong>{message(state.gameCompatibility[modal.game.id]!.twoPlayer)}</strong>
                    {state.gameCompatibility[modal.game.id]!.notes.map((note,i)=><p key={i}>{message(note)}</p>)}
                  </div>
                )}
                {modal.game.setupNotes && (
                  <details>
                    <summary>{t("Einrichtung und Helfer")}</summary>
                    <p className="technical">{modal.game.setupNotes}</p>
                  </details>
                )}
                <details>
                  <summary>{t("Gespeicherter Startweg")}</summary>
                  <p className="technical">
                    {modal.game.executable}
                    <br />
                    {modal.game.arguments.join(" ")}
                    <br />
                    {t("Arbeitsordner:") + " "}
                    {modal.game.workingDirectory}
                  </p>
                </details>
                <div className="setting-row">
                  <strong>{t("Bildformat der Gun")}</strong>
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
                    <ImageIcon size={17} />
                    {" " + t("Eigenes Cover w\u00E4hlen")}
                  </button>
                  <button
                    className="secondary"
                    disabled={modal.game.status === "needs-setup"}
                    onClick={() => {
                      send("mark-tested", { id: modal.game.id });
                      setModal(null);
                    }}
                  >
                    <Check size={17} />
                    {" " + t("Als spielbar best\u00E4tigen")}
                  </button>
                </div>
                <p className="detail-note">
                  {t(
                    "\u201ESpielbar best\u00E4tigt\u201C bedeutet, dass du das Spiel mit deinem Setup gepr\u00FCft hast.",
                  )}
                </p>
                <button
                  className="text-button danger"
                  onClick={() => {
                    send("remove-game", { id: modal.game.id });
                    setModal(null);
                  }}
                >
                  {t("Aus Bibliothek entfernen")}
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
          aria-label={t("Zieltest")}
        >
          <div className="target-test-header">
            <div>
              <span className="eyebrow">
                {t("SPIELER") + " "}
                {modal.player} ·{" "}
                {state.native ? t("EINGABETEST") : t("BEDIENPROBE")}
              </span>
              <h2>
                {testStep < 5
                  ? t("Triff das leuchtende Ziel.")
                  : t("F\u00FCnf Ziele getroffen.")}
              </h2>
              <p>
                {state.native
                  ? t("Nur Eingaben der zugeordneten Gun werden gewertet.")
                  : t(
                      "Vorschau mit Maus oder Touch \u2014 keine Ger\u00E4tepr\u00FCfung.",
                    )}
              </p>
            </div>
            <button className="secondary" onClick={() => setModal(null)}>
              <X size={18} />
              {" " + t("Beenden")}
            </button>
          </div>
          {testStep < 5 ? (
            <button
              className="test-target"
              style={{
                left: targets[testStep][0] * 100 + "%",
                top: targets[testStep][1] * 100 + "%",
              }}
              aria-label={t("Ziel {0}", testStep + 1)}
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
                  ? t("Eingabetest abgeschlossen.")
                  : t("Bedienprobe abgeschlossen.")}
              </h2>
              <p>
                {t("Gr\u00F6\u00DFte Abweichung der angenommenen Treffer:")}{" "}
                {Math.round(Math.max(...testErrors, 0))}
                {" " + t("px")}
                <br />
                {testMisses}
                {" " + t("Sch\u00FCsse au\u00DFerhalb des Zielbereichs.")}
              </p>
              <p>
                {t(
                  "Dieser Test best\u00E4tigt keine Firmwarekalibrierung und misst keine Eingabelatenz.",
                )}
              </p>
              <button className="primary" onClick={() => setModal(null)}>
                {t("Zur\u00FCck zu meinen Guns")}
              </button>
            </div>
          )}
          <div className="target-progress" aria-live="polite">
            {testStep}
            {" " + t("/ 5 Ziele \u00B7") + " "}
            {testMisses}
            {" " + t("Fehlsch\u00FCsse")}
          </div>
        </div>
      )}
      {session && (
        <div className="session-banner">
          <Gamepad2 size={20} />
          <span>{t("Ein Spiel l\u00E4uft.")}</span>
          <button className="secondary" onClick={() => send("show-overlay")}>
            {t("Spielmen\u00FC \u00F6ffnen")}
          </button>
          <button className="secondary" onClick={() => send("end-game")}>
            {t("Spiel beenden")}
          </button>
        </div>
      )}
      {picker && <FilePicker picker={picker} send={send} />}
      {keyboard && (
        <ArcadeKeyboard
          title={
            keyboard === "search"
              ? t("Spiel suchen")
              : t("Cover-Schl\u00FCssel eingeben")
          }
          initial={keyboard === "search" ? search : coverKey}
          secret={keyboard === "cover"}
          cancel={() => setKeyboard(null)}
          done={(value) => {
            if (keyboard === "search") setSearch(value);
            else setCoverKey(value);
            setKeyboard(null);
          }}
        />
      )}
    </div>
  );
}
createRoot(document.getElementById("root")!).render(<App />);
