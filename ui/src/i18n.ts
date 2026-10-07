import english from "../../localization/en.json";

export type Language = "en" | "de";
const messages: Record<string, string> = english;
export const normalizeLanguage = (value: unknown): Language =>
  value === "de" ? "de" : "en";
// The Windows library is authoritative. localStorage is only for the browser preview.
let language: Language = "en";
export function getLanguage() {
  return language;
}
export function setLanguage(value: unknown) {
  language = normalizeLanguage(value);
  document.documentElement.lang = language;
}
export function t(key: string, ...values: unknown[]): string {
  const text = language === "de" ? key : (messages[key] ?? key);
  return text.replace(/\{(\d+)\}/g, (_, index) =>
    String(values[Number(index)] ?? ""),
  );
}
// Translate only known application messages, preserving paths, titles and imported notes.
// Allows stored status messages to follow a language switch without rerunning hardware setup.
const patterns = Object.entries(messages)
  .filter(([de, en]) => de !== en)
  .map(([de, en]) => {
    const pattern = (s: string) =>
      new RegExp(
        "^" +
          s
            .split(/(\{\d+\})/)
            .map((p) =>
              /^\{\d+\}$/.test(p)
                ? "([\\s\\S]*?)"
                : p.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"),
            )
            .join("") +
          "$",
        "u",
      );
    return { de, en, dePattern: pattern(de), enPattern: pattern(en) };
  });
const prefixes = Object.entries(messages)
  .filter(([de, en]) => de !== en && (de.endsWith(" ") || de.endsWith(": ")))
  .sort(
    (a, b) =>
      Math.max(b[0].length, b[1].length) - Math.max(a[0].length, a[1].length),
  );
const suffixes = [
  " fehlt",
  " nicht lesbar",
  " hat ein falsches oder nicht lesbares Dateiformat",
  ": .NET-Konfiguration nicht lesbar",
  " nicht aufgelöst (Profil oder lokale Installation prüfen)",
  " separat prüfen",
].map((de) => [de, messages[de]]);
export function message(value: string): string {
  if (messages[value]) return t(value);
  for (const item of patterns) {
    const match = (language === "en" ? item.dePattern : item.enPattern).exec(
      value,
    );
    if (match)
      return (language === "en" ? item.en : item.de).replace(
        /\{(\d+)\}/g,
        (_, i) => match[Number(i) + 1] ?? "",
      );
  }
  // Import / dependency reports may append a private path to a known message prefix.
  for (const [de, en] of prefixes) {
    const source = language === "en" ? de : en;
    if (value.startsWith(source))
      return (language === "en" ? en : de) + value.slice(source.length);
  }
  for (const [de, en] of suffixes) {
    const source = language === "en" ? de : en;
    if (value.endsWith(source))
      return value.slice(0, -source.length) + (language === "en" ? en : de);
  }
  return value;
}
export const locale = () => (language === "de" ? "de-DE" : "en-GB");
