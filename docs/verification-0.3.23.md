# Input recovery and control coverage

The regression checks cover serial transmit drain and final-command settling, Raw Input ownership, session unlock/resume classification, and clearing held buttons for both bridge players. Windows 11 returns `INPUTSINK` in queried registration flags while omitting `DEVNOTIFY`; the ownership check accounts for that observed behavior to avoid repeated registration.

Dolphin checks write and read actual per-title P1/P2 profiles for Umbrella Chronicles, Darkside Chronicles, Overkill, Ghost Squad and Link's Crossbow Training. They resolve weapon selection and gesture expressions to physical RS3 controls, then remove a P2 action and verify that the audit identifies it. Existing Extraction coverage checks independent float aim, action combinations and retained calibration values.

TeknoParrot checks separate Change and Option actions from trigger/reload/secondary bindings. The reviewed adapters also cover Scarlet Dawn sub-trigger/controller-select and Luigi's Mansion vacuum controls. The audit reads the selected vendor profile and excludes cabinet service/volume/test placeholders from gameplay requirements.

`--audit-controls` reads the library without changing profiles. `--repair-controls` applies reviewed adapters with backups/history and saves `controls-audit.json` locally. A launch-time audit retains earlier library entries. Automatic repairs exposes missing or unreviewed assignments in Settings.

A mapping check does not prove complete gameplay, aiming accuracy, independent physical P2 input or post-resume hardware recovery. Those require a local gun test. Unreadable/unreviewed titles remain identified instead of being marked complete.
