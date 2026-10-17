# Debug Room

[Documentation](../README.md) / Players

Press **F2** to open Debug Room, or change its binding in [Controls](controls.md). Some tools are available only while a session has the state they need. Distinguish settings for the current game from isolated previews and laboratory scenarios.

## Sections

| Section | Purpose |
| --- | --- |
| Battle | Settings for the running game, including encounter/ATB preferences and active battle outcomes. |
| UI | Text scale, font preferences and interface presentation. |
| Audio | Music and effect volume preferences. |
| Controls | Keyboard and controller bindings. |
| Names | Edit names in the current session when naming is available. |
| Automatic play | Start, pause, resume, stop or reproduce automated exploration. |
| Menu gallery | Inspect isolated menu fixtures. |
| Battle laboratory | Edit and run an isolated battle scenario. |

Settings pages persist their supported host preferences automatically. A setting in an isolated gallery fixture does not implement the original game's corresponding option. See [saves and settings](saves.md) for storage and persistence boundaries.

## Edit names

The Names page edits characters, Guardian Forces and other supported named subjects. Select a name, enter a draft using the keyboard or character grid, then Save. Cancel discards the draft; it does not revert earlier saved changes. Names support 1–20 Unicode text elements, with blank names, invalid Unicode and control characters rejected.

This edits the running session without advancing simulation frames. Save the game to retain the changes in a game snapshot. Naming is available in an eligible field or world session, and is disabled during active battles, title/movie sessions and faulted fields.

## Battle settings and laboratory

The Battle section applies its settings to the current game. **Instant victory** and **Instant defeat** require an active battle. When a laboratory battle is open, these actions affect that displayed laboratory battle; otherwise they affect the game-owned battle.

Opening the laboratory initializes its editor from the current party and a recent eligible opponent. If there is no eligible encounter, it uses the procedural practice Dummy; at the title, party defaults come from New Game data. Laboratory edits remain separate from the running game's state.

Expand an actor row to edit supported fields; collapse it again to keep the scenario readable. Preset Save and Load are explicit actions. Loading a preset updates the editor and does not start a battle. The three preset slots use `battle-presets.json`, separate from ordinary game snapshots.

The laboratory exposes implemented battle behavior. It does not establish full original combat parity or support for every original statistic and ability.

## Previews and automation

[Automatic exploration](automatic-exploration.md) uses the same controller in Debug Room and in headless mode. Its coverage and failure snapshots belong to that run.

Developers can render registered UI screens without opening a graphics window:

```powershell
dotnet run --project src/Memoria.FF8.App -c Release -- --menu-preview --screen debug-room --output ./preview/debug-room.png
```

See the [command-line reference](../reference/command-line.md) for available preview screens and the [UI framework](../developers/ui-framework.md) for canvas and layout APIs. CPU previews render fixtures or supplied snapshots to check layout and presentation. Gameplay and GPU rendering need separate checks.
