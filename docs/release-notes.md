Player changes now reach the actual game profiles more reliably, and the system filter works with gun input.

- Clear obsolete TeknoParrot P1/P2 routes when a gun sits out or changes player slot.
- Remove old game routes when an action is unassigned or its input device is unavailable.
- Use the Vyper’s learned D-pad Down for weapon change when the selector is reserved for recoil.
- Apply the same Dolphin model and bridge policy in bulk repair and game launch, including Vyper and XGunner.
- Flag shared RetroArch keyboard codes in the controls check; different mouse indices alone do not isolate equal keyboard keys.
- Choose a library system with large gun-operable buttons, and keep keyboard navigation working across local and Remote Desktop changes.
- Keep explicit exit controls available in the aim test.

Validation includes core regression checks, the existing UI identity cases, a Windows build, Claude source reviews and focused rendered UI checks. Physical aiming, recoil and cooperative gameplay require a local hardware test.
