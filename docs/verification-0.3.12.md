# Verification: 0.3.12

The UI regression sends menu and keyboard events and an aimed trigger click while Gun Studio is testing buttons. The page must stay in Gun Studio, controls must still highlight, and legacy context menus must be suppressed. Ending the native test must restore menu navigation.

The native Windows input path retains visualization and explicit binding capture while suppressing menu dispatch and the Start/Coin emergency gesture. The physical trigger escape uses a monotonic ten-second hold, independent of its assigned menu action. Starting a game clears the test state.

RS3 calibration commands and target order were compared with the manufacturer calibration source. The module sends commands but provides no success acknowledgement. Aim accuracy, calibration persistence and physical trigger holds must be tested directly at the attached screen. Hardware firmware shortcuts cannot be suppressed by the application.

The RS3 HID usage-1 trigger also highlights the physical trigger in joystick mode. Other HID controls are not inferred from their assigned action. UI button-test instructions use a vertical layout to remain readable beside the gun diagram.
