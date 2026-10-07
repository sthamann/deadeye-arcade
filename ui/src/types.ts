import { t } from "./i18n";
export type Game = {
  id: string;
  title: string;
  platform: string;
  executable: string;
  arguments: string[];
  workingDirectory: string;
  source: string;
  sourcePath: string;
  status: string;
  cover: string | null;
  favorite: boolean;
  aspect: string;
  lastPlayed: string | null;
  previewVideo?: string | null;
  screenshot?: string | null;
  logo?: string | null;
  setupIssues?: string[];
  requiredFiles?: string[];
  priority?: number;
  players?: string | null;
  setupNotes?: string | null;
  helpers?: {
    executable: string;
    arguments: string[];
    workingDirectory: string;
  }[];
};
export type Binding = {
  player: number;
  mouseId: string;
  keyboardId: string | null;
  serialPort: string | null;
  systemId?: string;
  physicalId?: string | null;
  buttonMap?: Record<string, string> | null;
  controlMap?: Record<string,string> | null;
  feedback?: {
    recoil: boolean;
    rumble: boolean;
    offscreenReload: boolean;
    aspect: string;
  } | null;
  softwareConfigured?: boolean;
};
export type Device = {
  id: string;
  name: string;
  kind: string;
  retroShooter: boolean;
};
export type PhysicalGun = {
  id: string;
  name: string;
  systemId: string;
  identityEvidence: string;
  inputIds: string[];
  mouseId: string | null;
  keyboardId: string | null;
  port: string | null;
  driverHealthy: boolean;
  liveInputAvailable?: boolean;
  issues: string[];
};
export type GunSystem = {
  id: string;
  name: string;
  connection: string;
  detection: string;
  software: string;
  guide: string;
};
export type GunSignal = {
  player: number;
  token: string;
  down: boolean;
  action: string;
};
export type State = {
  update?: { status: string; progress: number; error: string; checkedAt?: string | null; release?: null | {version: string; notes: string; page: string} };
  guns?: PhysicalGun[];
  gunSystems?: GunSystem[];
  gunIssues?: Record<string, string>;
  gunSignals?: Record<string, string>;
  gunSoftware?: {
    id: string;
    downloaded: boolean;
    status: string;
  }[];
  learning?: null | {
    player: number;
    action: string | null;
    control?: string | null;
  };
  games: Game[];
  bindings: Binding[];
  devices: Device[];
  ports: string[];
  settings: {
    startWithWindows: boolean;
    fullscreen: boolean;
    hasCoverKey: boolean;
    language: "en" | "de";
    checkForUpdates?: boolean;
  };
  bindingStage: null | {
    player: number;
    stage: string;
  };
  version: string;
  remoteSession?: boolean;
  calibrationTool?: string | null;
  installations: {
    kind: string;
    name: string;
    path: string;
  }[];
  dependencies?: null | {
    time: string;
    games: number;
    checkedBinaries: number;
    uncheckedCount: number;
    uncheckedFiles: string[];
    packages: {
      id: string;
      name: string;
      missing: boolean;
      games: string[];
      dlls: string[];
    }[];
  };
  native: boolean;
};
export type Input = {
  deviceId: string;
  kind: string;
  player: number;
  x?: number;
  y?: number;
  buttons?: number;
  key?: number;
  down?: boolean;
};
declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void;
        addEventListener: (
          type: string,
          callback: (event: {
            data: {
              type: string;
              payload: any;
            };
          }) => void,
        ) => void;
        removeEventListener: (
          type: string,
          callback: (event: {
            data: {
              type: string;
              payload: any;
            };
          }) => void,
        ) => void;
      };
    };
  }
}
export const empty: State = {
  games: [],
  bindings: [],
  devices: [],
  ports: [],
  settings: {
    startWithWindows: false,
    fullscreen: true,
    hasCoverKey: false,
    language: "en",
  },
  bindingStage: null,
  version: "0.3.5",
  installations: [],
  native: !!window.chrome?.webview,
};
export const examples: Game[] = [
  {
    title: t("House of the Dead: Scarlet Dawn"),
    platform: "TeknoParrot",
    aspect: "16:9",
  },
  { title: t("Time Crisis 5"), platform: "TeknoParrot", aspect: "16:9" },
  { title: t("Jurassic Park Arcade"), platform: "TeknoParrot", aspect: "16:9" },
  { title: t("Point Blank"), platform: "MAME", aspect: "4:3" },
  { title: t("Aliens: Armageddon"), platform: "TeknoParrot", aspect: "16:9" },
  {
    title: t("The House of the Dead 4"),
    platform: "TeknoParrot",
    aspect: "16:9",
  },
  { title: t("Operation G.H.O.S.T."), platform: "TeknoParrot", aspect: "16:9" },
  { title: t("Virtua Cop 3"), platform: "TeknoParrot", aspect: "4:3" },
].map((g, i) => ({
  ...g,
  id: "demo-" + i,
  executable: "",
  arguments: [],
  workingDirectory: "",
  source: "demo",
  sourcePath: "",
  status: "demo",
  cover: null,
  favorite: false,
  lastPlayed: null,
}));
export function badge(g: Game) {
  return g.status === "demo"
    ? t("Vorschau")
    : g.status === "tested"
      ? t("Von dir best\u00E4tigt")
      : g.status === "needs-setup"
        ? t("Einrichtung n\u00F6tig")
        : t("Erster Spieltest offen");
}
