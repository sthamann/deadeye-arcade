import { t, message, getLanguage } from "./i18n";
import { useState } from "react";
import { ArrowLeft, FolderOpen, X, File, Check, Delete } from "lucide-react";
export type PickerState = {
  title: string;
  foldersOnly: boolean;
  page: {
    path: string;
    parent: string | null;
    entries: {
      name: string;
      path: string;
      directory: boolean;
    }[];
    warning: string | null;
  };
  shortcuts: {
    name: string;
    path: string;
  }[];
};
export function FilePicker({
  picker,
  send,
}: {
  picker: PickerState;
  send: (type: string, payload?: unknown) => void;
}) {
  return (
    <div className="modal-backdrop">
      <section
        className="modal file-picker"
        role="dialog"
        aria-modal="true"
        aria-label={message(picker.title)}
      >
        <div className="section-heading">
          <h2>{message(picker.title)}</h2>
          <button
            className="icon-button"
            aria-label={t("Dateiauswahl schlie\u00DFen")}
            onClick={() => send("cancel-picker")}
          >
            <X />
          </button>
        </div>
        <div className="picker-shortcuts">
          {picker.shortcuts.map((s) => (
            <button
              className="secondary compact"
              key={s.path}
              onClick={() => send("browse-path", { path: s.path })}
            >
              <FolderOpen size={18} />
              {s.name}
            </button>
          ))}
        </div>
        <div className="picker-path">
          <button
            className="secondary compact"
            disabled={!picker.page.parent}
            onClick={() => send("browse-path", { path: picker.page.parent })}
          >
            <ArrowLeft size={18} />
            {t("Eine Ebene h\u00F6her")}
          </button>
          <span>{picker.page.path}</span>
        </div>
        {picker.page.warning && <p>{message(picker.page.warning)}</p>}
        <div className="picker-entries">
          {picker.page.entries.length ? (
            picker.page.entries.map((e) => (
              <button
                key={e.path}
                onClick={() =>
                  send(e.directory ? "browse-path" : "choose-path", {
                    path: e.path,
                  })
                }
              >
                {e.directory ? <FolderOpen size={22} /> : <File size={22} />}
                <span>{e.name}</span>
              </button>
            ))
          ) : (
            <p>{t("Keine passenden Eintr\u00E4ge in diesem Ordner.")}</p>
          )}
        </div>
        {picker.foldersOnly && (
          <button
            className="primary"
            onClick={() => send("choose-path", { path: picker.page.path })}
          >
            <Check size={20} />
            {t("Diesen Ordner verwenden")}
          </button>
        )}
      </section>
    </div>
  );
}
export function ArcadeKeyboard({
  title,
  initial,
  secret,
  done,
  cancel,
}: {
  title: string;
  initial: string;
  secret?: boolean;
  done: (value: string) => void;
  cancel: () => void;
}) {
  const [value, setValue] = useState(initial);
  const [upper, setUpper] = useState(false);
  return (
    <div className="modal-backdrop">
      <section
        className="modal arcade-keyboard"
        role="dialog"
        aria-modal="true"
        aria-label={title}
      >
        <div className="section-heading">
          <h2>{title}</h2>
          <button
            className="icon-button"
            aria-label={t("Tastatur schlie\u00DFen")}
            onClick={cancel}
          >
            <X />
          </button>
        </div>
        <input
          aria-label={t("Texteingabe")}
          type={secret ? "password" : "text"}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          autoComplete="off"
        />
        <div className="keyboard-keys">
          {(getLanguage() === "de"
            ? "1234567890qwertzuiopasdfghjklyxcvbnmäöüß-_."
            : "1234567890qwertyuiopasdfghjklzxcvbnm-_."
          )
            .split("")
            .map((c) => (
              <button
                key={c}
                onClick={() =>
                  setValue((v) => v + (upper ? c.toUpperCase() : c))
                }
              >
                {upper ? c.toUpperCase() : c}
              </button>
            ))}
        </div>
        <div className="keyboard-actions">
          <button
            className="secondary"
            onClick={() => setUpper(!upper)}
            aria-pressed={upper}
          >
            {t("Aa")}
          </button>
          <button
            className="secondary"
            onClick={() => setValue((v) => v + " ")}
          >
            {t("Leerzeichen")}
          </button>
          <button
            className="secondary"
            aria-label={t("Letztes Zeichen l\u00F6schen")}
            onClick={() => setValue((v) => v.slice(0, -1))}
          >
            <Delete size={20} />
          </button>
          <button className="secondary" onClick={() => setValue("")}>
            {t("Leeren")}
          </button>
          <button className="primary" onClick={() => done(value)}>
            <Check size={18} />
            {t("\u00DCbernehmen")}
          </button>
        </div>
      </section>
    </div>
  );
}
