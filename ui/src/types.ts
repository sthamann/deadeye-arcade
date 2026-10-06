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
  helpers?: { executable: string; arguments: string[]; workingDirectory: string }[];
};
export type Binding = {
  player: number;
  mouseId: string;
  keyboardId: string | null;
  serialPort: string | null;
};
export type Device = {
  id: string;
  name: string;
  kind: string;
  retroShooter: boolean;
};
export type State = {
  games: Game[];
  bindings: Binding[];
  devices: Device[];
  ports: string[];
  settings: {
    startWithWindows: boolean;
    fullscreen: boolean;
    hasCoverKey: boolean;
  };
  bindingStage: null | { player: number; stage: string };
  version: string;
  remoteSession?: boolean;
  calibrationTool?: string | null;
  installations: { kind: string; name: string; path: string }[];
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
          callback: (event: { data: { type: string; payload: any } }) => void,
        ) => void;
        removeEventListener: (
          type: string,
          callback: (event: { data: { type: string; payload: any } }) => void,
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
  settings: { startWithWindows: false, fullscreen: true, hasCoverKey: false },
  bindingStage: null,
  version: "0.3.0",
  installations: [],
  native: !!window.chrome?.webview,
};
export const examples: Game[] = [
  {
    title: "House of the Dead: Scarlet Dawn",
    platform: "TeknoParrot",
    aspect: "16:9",
  },
  { title: "Time Crisis 5", platform: "TeknoParrot", aspect: "16:9" },
  { title: "Jurassic Park Arcade", platform: "TeknoParrot", aspect: "16:9" },
  { title: "Point Blank", platform: "MAME", aspect: "4:3" },
  { title: "Aliens: Armageddon", platform: "TeknoParrot", aspect: "16:9" },
  { title: "The House of the Dead 4", platform: "TeknoParrot", aspect: "16:9" },
  { title: "Operation G.H.O.S.T.", platform: "TeknoParrot", aspect: "16:9" },
  { title: "Virtua Cop 3", platform: "TeknoParrot", aspect: "4:3" },
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
    ? "Vorschau"
    : g.status === "tested"
      ? "Von dir bestätigt"
      : g.status === "needs-setup"
        ? "Einrichtung nötig"
        : "Erster Spieltest offen";
}
