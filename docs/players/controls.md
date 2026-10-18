# Controls

These are the default bindings. Remapping in **Config → Controls** changes the hints shown by the engine. Controls are shared across saves for the current user, rather than stored in each gameplay snapshot.

## Keyboard and controller

Controller names below use physical positions. The button-label setting can display Generic, Xbox, PlayStation or Nintendo names; changing labels does not exchange button positions.

| Action | Keyboard | Controller |
| --- | --- | --- |
| Move / menu direction | Arrow keys | D-pad or left stick |
| Confirm / talk | X | South button (Xbox A / PlayStation Cross) |
| Cancel / walk modifier | C | East button (Xbox B / PlayStation Circle) |
| Menu | V | North button (Xbox Y / PlayStation Triangle) |
| Card / detail | S | West button (Xbox X / PlayStation Square) |
| Select | J | Back / View / Share |
| Pause | A | Start / Menu / Options |
| Escape battle (left) | D | Left trigger |
| Camera mode / escape battle (right) | F | Right trigger |
| Rotate left / previous character | H | Left shoulder |
| Rotate right / next character | G | Right shoulder |

The right stick supplies camera input and car controls in the supported world contexts. Tilt the left stick gently to walk; the default run threshold is 50%. Digital movement uses the original button semantics. Input availability still depends on the current scene and script: a binding does not guarantee that an action is valid everywhere.

In supported battle contexts, Rotate right is also the gunblade / Shot trigger, and Card is the GF Boost input. Menu detail and character paging use these shared actions too.

## Host shortcuts

These shortcuts are keyboard-only by default. They can also be assigned to controller buttons or combinations in Controls.

| Shortcut | Default key | Effect |
| --- | --- | --- |
| Event speed | F1 | Toggle x1 / x3 for ordinary field and world simulation. Movies remain at normal speed. This setting is not saved in the gameplay snapshot. |
| Debug room | F2 | Open the host debug UI where available. |
| Instant victory | F3 | Debug command for an active battle. |
| Battle editor | F4 | Open battle debug controls. |
| Quick save | F5 | Write the configured quick-save snapshot. |
| Save menu | F6 | Open the player save interface. |
| Quick load | F9 | Restore the configured quick-save snapshot. |
| Load menu | F10 | Open the player load interface. |
| Advance turn | Space | Advance the battle HUD in TurnBased mode. |
| UI tab | Tab | Move between supported UI tabs. |
| UI page | Page Up / Page Down | Page supported UI lists. |

Quick save uses `%LOCALAPPDATA%/Memoria.FF8/checkpoint.json` by default. Override it with [`--save-state`](../reference/command-line.md). See [saves](saves.md) for the separate player-save interface, and [debug room](debug-room.md) for diagnostic features.

## Movies and naming

Hold the currently bound **Cancel** input for one second to request a movie skip. A progress hint appears while holding. A brief press does not complete the gesture. The gesture follows the Cancel binding; there is no separate movie-skip binding.

When a name-entry interface accepts keyboard text, type the name and use its on-screen editing controls. Ordinary keyboard action bindings are suppressed during text entry. Enter always confirms, Escape always cancels, and the arrow keys always move in their respective directions, alongside your custom bindings. These keys cannot be assigned to other actions, including as part of a combination. Tab and Page Up / Page Down also provide navigation in settings and text-entry contexts.

## Remapping and device settings

Open **Config → Controls** and choose Keyboard or Controller. Each row shows an action and its current bindings. Confirm or click the row to capture a new binding: release all controls, hold the desired combination together, and release it to finish. Escape cancels capture. Press the button assigned to **Card / Details**, shown in Help, to open advanced options for the selected action: capture, clear binding or back.

If the exact combination already belongs to another action, choose whether to keep the current bindings or replace the conflicting bindings. Controller Confirm and Cancel must remain bound; permanent keyboard alternatives remain available even after clearing custom bindings. You can also restore defaults for one device. Changes save automatically; the other device keeps its bindings.

Controller settings include independent left/right radial dead zones, the run threshold, button labels and vibration. Defaults are 20% dead zones, 50% run threshold, automatic button labels and vibration enabled. Stick directions are used for movement/camera navigation and cannot be captured as action bindings.

The profile is stored at `%LOCALAPPDATA%/Memoria/FF8/controls.json`. A missing profile uses defaults. Settings remain independent of the installed game directory and the active save path. If a legacy settings file exists beside the configured quick-save path, it is imported only when the global destination is missing.

After regaining focus, changing the profile or reconnecting a controller, release held buttons and return sticks to neutral before pressing again. This prevents a held input from triggering an unintended action. Keyboard input is ignored while the window is inactive.

Sources: [default profile and button names](../../src/Memoria.FF8.App/ControlsProfile.cs), [input sampling](../../src/Memoria.FF8.App/KeyboardInput.cs), [controls UI](../../src/Memoria.FF8.App/GameUi/MenuController.Controls.cs), [global settings paths](../../src/Memoria.FF8.App/GlobalSettingsPaths.cs), [movie gesture](../../src/Memoria.FF8.App/MovieSkipGesture.cs) and [speed clock](../../src/Memoria.FF8.App/GameplayClock.cs).
