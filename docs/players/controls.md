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

The right stick supplies camera input in supported world contexts. In a car or while driving Garden, Card / detail accelerates and Cancel / walk reverses; left/right or the left stick steers. Up/down, including remapped direction keys such as W/S, also drives forward/backward. The separate throttle buttons take priority over these directional alternatives. The right stick's vertical axis also controls forward/reverse. These actions follow your current bindings, including the buttons shown by the driving tutorial. Tilt the left stick gently to walk on foot; the default run threshold is 50%. Input availability still depends on the current scene and script: a binding does not guarantee that an action is valid everywhere.

In supported battle contexts, Rotate right is also the gunblade / Shot trigger, and Card is the GF Boost input. Menu detail and character paging use these shared actions too.

In battle, **Menu** (V / North button) cycles ready characters and recalculates Limit availability, including when only one character is ready. When the first command shows `>`, hold **Right** to reveal the character's Limit and confirm while holding it. Releasing Right before confirmation returns to the ordinary command. Confirming Renzokuken or Duel opens target selection directly; the other Limits open their spell, ammunition or action choices. Cancel returns from a target or choice without recalculating the crisis level.

Set **Config → Interface → Cursor → Memory** to restore each character's last accepted battle command and each command's last accepted action during the current battle. For example, after **Draw → Blizzard → Stock**, the next turn starts on Draw; choosing a source restores Blizzard, and confirming it restores Stock. Stock/Cast choices are remembered separately for each drawn spell. Valid targets are also restored. If a remembered action or target is no longer available, the cursor starts at the first available entry. Cancelled selections and rejected commands do not replace the remembered action. **Initial**, the default, resets cursors. This preference is shared across saves; battle selections reset when a new battle HUD is created or a battle is loaded. Limits retain their Right-to-reveal behavior.

## Host shortcuts

These shortcuts use the default bindings below. They can also be assigned to controller buttons or combinations in Controls.

| Shortcut | Default key | Effect |
| --- | --- | --- |
| Event speed | F1 | Toggle x1 / x3 for ordinary field and world simulation. Movies remain at normal speed. This setting is not saved in the gameplay snapshot. |
| Debug room | F2 | Open the host debug UI where available. |
| Instant victory | F3 | Debug command for an active battle. |
| Battle editor | F4 | Open battle debug controls. |
| GF form finisher Skip | F8 / right stick click | Complete the active form's shortened Hell Fire, preserving earned Boost. |
| Quick save | F5 | Write the configured quick-save snapshot. |
| Save menu | F6 | Open the player save interface. |
| Quick load | F9 | Open the quick-save picker. |
| Load menu | F10 | Open the player load interface. |
| Advance turn | Space | Advance the battle HUD in TurnBased mode. |
| UI tab | Tab | Move between supported UI tabs. |
| UI page | Page Up / Page Down | Page supported UI lists. |

Quick saves use `%LOCALAPPDATA%/Memoria.FF8/player-saves/`. In the Save or Load menu, Card / detail starts a transfer of the selected save between Memoria and either original-game bank. See [saves](saves.md) for bank capacities, destination selection and original save-folder discovery, and [debug room](debug-room.md) for diagnostic features.

## Display settings

Open **Config → Display** to choose **Field FPS: 30 or 60** (default 60). At 60 FPS, field movement and animation interpolate between the original simulation frames. Scripts, movement rules and gameplay timing retain their original rate. Movies retain their own playback timing. **V-Sync** can limit presentation further when the display cannot reach the selected rate.

**Show FPS**, also available under **Debug Room → UI**, displays completed presentations per real second as **FPS** and visible RGB changes as **Changes/s**. A still scene can show 60 FPS and 0 Changes/s. Repeated identical frames and the overlay itself do not count as changes. Both measurements include rendering, pacing and V-Sync waits; neither is the configured limit or simulation speed.

## Graphics

**Config → Graphics → Anti-aliasing** offers **Off**, **MSAA 2x**, **MSAA 4x** and **MSAA 8x**. The default is **MSAA 8x**. Changes apply immediately and are shared by all saves. The graphics device uses a supported sample count when the requested quality is unavailable.

The same setting is stored in the `Graphics` section of `%LOCALAPPDATA%/Memoria/FF8/ui-settings.json`. Set `MsaaSamples` to `0`, `2`, `4` or `8`; existing configuration files without this section retain the default. Close the game before editing the file directly.

```json
"Graphics": {
  "MsaaSamples": 8
}
```

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
