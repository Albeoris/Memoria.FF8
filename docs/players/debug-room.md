# Debug Room

[Documentation](../README.md) / Players

Press **F2** to open Debug Room, or change its binding in [Controls](controls.md). Some tools are available only while a session has the state they need. Distinguish settings for the current game from isolated previews and laboratory scenarios.

## Sections

| Section | Purpose |
| --- | --- |
| [Models](../modding/model-mods.md) | Open the model catalog directly; search, compare variants and exchange GLB files. |
| Battle | Laboratory, encounter execution policy, displayed battle status and explicit Win/Lose actions. |
| Scene | Open the inspector directly; Layers, Objects and Events remain local tabs. |
| UI tools | Open the fixture menu gallery directly; Reset fixture data affects only previews. |
| Session | Open the supported-name list directly. |
| Gameplay rules | Edit numeric rewards, magic consumption, damage limits, Rare Item probabilities and Scan rotation. |
| Automation | Start, pause, resume, stop or replay automated exploration. |
| Shortcuts | Keyboard/controller bindings for Debug Room, battle laboratory and battle victory only. |

Player preferences belong in Config. Debug Room has no second editor for volume, fonts, difficulty, random encounters, player ATB mode or gunblade triggering. The gallery is labelled Preview / Fixture and its changes stay isolated from live owners. See [saves and settings](saves.md) for persistence boundaries.

## Gameplay rules

One editor contains three pages: **Rewards & Draw**, **Magic & damage**, and **Rare Item & Scan**. Numeric fields open a keypad and also accept keyboard input. Previous/Next keeps the draft; Apply commits it and Cancel discards it. Save the game to retain applied values. An active battle keeps its captured rules until it ends.

Draw bonus (0–100) adds to successful draws only; failure still awards nothing and spell stock remains capped at 100. AP, character EXP and GF EXP have separate multipliers (0–100, default 1). The imported character-EXP option does not change GF EXP. Field interactions already in progress retain their captured bonus. World-map Draw acquisition is not implemented yet.

**Infinite magic** avoids stock consumption without adding cooldowns. It requires an accessible stocked spell and takes precedence over the separate **Limited magic** rules. **Maximum damage** is a strict positive HP-damage cap from 0 to 100000, default 9999, including attacks with the native break-limit flag. It does not change healing, HP costs or direct HP assignments.

The four Rare Item slot probabilities must total exactly 100%. Selection retains the original 256-value random clock, so custom percentages have that granularity. Free Scan rotation removes directional restrictions for every model. Save-anywhere and dream junction carry are intrinsic and have no disabling controls.

Approved executable-mod behavior may lock individual fields. The editor names the controlling package; change its mod option to release the field. Personal values are retained separately and return when the override is removed. Unknown HEXT bytes require individual review; rejected executable options cannot be activated. Ordinary cosmetic file replacements use the normal resource loader.

## Edit names

The Session name list edits characters, Guardian Forces and other supported named subjects. Select a name, enter a draft using the keyboard or character grid, then Save. Cancel discards the draft; it does not revert earlier saved changes. Names support 1–20 Unicode text elements, with blank names, invalid Unicode and control characters rejected.

This edits the running session without advancing simulation frames. Save the game to retain the changes in a game snapshot. Naming is available in an eligible field or world session, and is disabled during active battles, title/movie sessions and faulted fields.

## Battle settings and laboratory

The Battle section shows whether actions target a **Game battle** or **Laboratory battle**. **Win current battle** and **Lose current battle** require that displayed battle to be active. **Encounter execution** selects Full combat or Automatic victory for the current game (the new-game default at Title). Choosing Automatic victory can finish an active or pending encounter and requires confirmation of that immediate consequence. Merely opening the page changes nothing. Save the game to retain session rules.

Opening the laboratory initializes its editor from the current party and a recent eligible opponent. If there is no eligible encounter, it uses the procedural practice Dummy; at the title, party defaults come from New Game data. Laboratory edits remain separate from the running game's state.

Use Scenario, Participants and Presets to keep the editor compact. Select an actor to edit Identity, Vitals and stats, Equipment and abilities, or Status effects. Status timing fields appear for enabled effects. Preset Save and Load are explicit actions. Loading a preset updates the editor and does not start a battle. The three preset slots use `battle-presets.json`, separate from ordinary game snapshots.

The laboratory exposes implemented battle behavior. It does not establish full original combat parity or support for every original statistic and ability.

## Previews and automation

Starting a new automated game or replaying a saved reproduction replaces the running session and requires confirmation when a live game exists. Pause/Resume and Stop retain their existing run behavior.

[Automatic exploration](automatic-exploration.md) uses the same controller in Debug Room and in headless mode. Its coverage and failure snapshots belong to that run.

Developers can render registered UI screens without opening a graphics window:

```powershell
dotnet run --project src/Memoria.FF8.App -c Release -- --menu-preview --screen debug-room --output ./preview/debug-room.png
```

See the [command-line reference](../reference/command-line.md) for available preview screens and the [UI framework](../developers/ui-framework.md) for canvas and layout APIs. CPU previews render fixtures or supplied snapshots to check layout and presentation. Gameplay and GPU rendering need separate checks.
