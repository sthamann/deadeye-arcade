# English and German UI

English is the default language for new installations and existing libraries that
have no `settings.language` value. Select **Settings → Language → English / Deutsch**.
The Windows library persists `en` or `de`; unrecognized stored values display English.
The browser preview has a separate localStorage preference.

`localization/en.json` is shared by the React interface and the native .NET/WPF app.
German source messages are keys. `t()` translates React text and formats numbered
placeholders; `I18n.T()` and `I18n.F()` cover native menus, messages and format strings.
The catalog is embedded into the core assembly, so the Windows app needs no external
translation file or translation service. No user data is sent for translation.

Language changes refresh module-level label factories and the native exit button.
They preserve library contents, input bindings, feedback settings and autostart.
The native game menu reads the current language when it opens.
Known cached status messages can change language without rerunning gun setup.
Imported titles, user notes, paths and manufacturer/emulator interfaces are not rewritten.

English uses a QWERTY on-screen keyboard; German uses QWERTZ with ä, ö, ü and ß.
API keys and typed input are preserved when changing language.

Regression checks cover first-run defaults, missing-field migration, formatted
native messages, cold library reload, frontend switching in both directions and
browser reload persistence. A target-Windows smoke test is required for packaged
WebView2 and WPF behavior.
