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
| Automation | Start, pause, resume, stop or replay automated exploration. |
| Shortcuts | Keyboard/controller bindings for Debug Room, battle laboratory and battle victory only. |

Player preferences belong in Config. Debug Room has no second editor for volume, fonts, difficulty, random encounters, player ATB mode or gunblade triggering. The gallery is labelled Preview / Fixture and its changes stay isolated from live owners. See [saves and settings](saves.md) for persistence boundaries.

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
