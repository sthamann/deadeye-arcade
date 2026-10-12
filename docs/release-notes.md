Deadeye Arcade 0.3.23 improves input recovery and per-player game controls.

- Re-registers Raw Input after session unlock, resume and Remote Desktop transitions, checks registration ownership and clears stale held buttons in both the frontend and the desktop companion.
- Waits for RS3 serial output to drain and settle before closing the connection, including the command that restores normal mouse input.
- Completes reviewed Dolphin mappings for Umbrella Chronicles, Darkside Chronicles, Overkill, Ghost Squad and Link's Crossbow Training. Both players receive native bridge buttons for weapon selection and gesture actions.
- Adds distinct RS3 bindings for TeknoParrot Change/Option actions and reads active game profiles into a local controls audit. Missing assignments and unreviewed title requirements remain visible in Automatic repairs.
- Distinguishes USB detection from actual gun button input. Failed serial setup no longer retains a stale configured flag.

Physical input, gameplay completeness and independent two-player operation still require an on-screen gun test; software checks do not mark those tests as completed.
