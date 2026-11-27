# Debug Room

[Documentation](../README.md) / Players

Press **F2** to open Debug Room, or change its binding in [Controls](controls.md). Some tools are available only while a session has the state they need. Distinguish settings for the current game from isolated previews and laboratory scenarios.

## Sections

| Section | Purpose |
| --- | --- |
| [Models](../modding/model-mods.md) | Open the model catalog directly; search, compare variants and exchange GLB files. |
| Battle | Weapons and Limit assignments, laboratory, encounter execution policy, displayed battle status and explicit Win/Lose actions. |
| Scene | Open the inspector directly; Layers, Objects and Events remain local tabs. |
| UI tools | Open the fixture menu gallery directly; Reset fixture data affects only previews. |
| Session | Open the supported-name list directly. |
| Gameplay rules | Edit numeric rewards, magic consumption, damage limits, Rare Item probabilities and Scan rotation. |
| Automation | Start, pause, resume, stop or replay automated exploration. |
| Shortcuts | Keyboard/controller bindings for Debug Room, battle laboratory and battle victory only. |

Player preferences belong in Config. Debug Room has no second editor for volume, fonts, difficulty, random encounters, player ATB mode or gunblade triggering. The gallery is labelled Preview / Fixture and its changes stay isolated from live owners. See [saves and settings](saves.md) for persistence boundaries.

## Recording low FPS

Press **F2** to open Debug Room, then select **Start FPS trace** and reproduce the slowdown. Opening Debug Room pauses gameplay. If you open it from another menu, starting the recording returns you to that menu. The indicator shows the size already written to disk.

Press **F2** again to stop and review the report. Select **Report performance** to prepare a ZIP and open a GitHub draft. Attach the ZIP, describe what you were doing when the frame rate dropped, and submit the issue. **Details** shows the original CSV path.

The CSV has one row per interval between presented frames. Compare `interval_ms` with the target frame duration (16.7 ms at 60 FPS). Use the scene and field or encounter ID to locate the slowdown. Update and draw timings include their respective subphases. Present time includes time waiting for the graphics backend. Pacing wait measures time the host spends waiting for its frame limit, and GC pause measures the change in process-wide garbage collection pause time. The ZIP also records the graphics adapter, renderer, resolution, VSync and configured frame limits.

## Gameplay rules

One editor contains three pages: **Rewards & Draw**, **Magic & damage**, and **Rare Item & Scan**. Numeric fields open a keypad and also accept keyboard input. Previous/Next keeps the draft; Apply commits it and Cancel discards it. Save the game to retain applied values. An active battle keeps its captured rules until it ends.

Draw bonus (0–100) adds to successful draws only; failure still awards nothing and spell stock remains capped at 100. AP, character EXP and GF EXP have separate multipliers (0–100, default 1). The imported character-EXP option does not change GF EXP. Field interactions already in progress retain their captured bonus. World-map Draw acquisition is not implemented yet.

**Infinite magic** avoids stock consumption without adding cooldowns. It requires an accessible stocked spell and takes precedence over the separate **Limited magic** rules. **Maximum damage** is a strict positive HP-damage cap from 0 to 100000, default 9999, including attacks with the native break-limit flag. It does not change healing, HP costs or direct HP assignments.

The four Rare Item slot probabilities must total exactly 100%. Selection retains the original 256-value random clock, so custom percentages have that granularity. Free Scan rotation removes directional restrictions for every model. Save-anywhere and dream junction carry are intrinsic and have no disabling controls.

Approved executable-mod behavior may lock individual fields. The editor names the controlling package; change its mod option to release the field. Personal values are retained separately and return when the override is removed. Unknown HEXT bytes require individual review; rejected executable options cannot be activated. Ordinary cosmetic file replacements use the normal resource loader.

## Textures

Choose **Textures** for current-scene, installation-wide or selected texture export. The captured scene uses the presentation before Debug Room opened. Search the shared asset tree, export ordinary PNGs or layered field ORA workspaces, and use **Import files** or **Import folder** to review edits and create or update a native Memoria mod. **Import and apply** publishes texture changes without advancing gameplay. Installed mod textures use normal watcher refresh; external working folders have one-action reimport. See [Texture authoring](../modding/texture-authoring.md) for image identity, field-layer rules and supported source contracts.

## Edit names

The Session name list edits characters, Guardian Forces and other supported named subjects. Select a name, enter a draft using the keyboard or character grid, then Save. Cancel discards the draft; it does not revert earlier saved changes. Names support 1–20 Unicode text elements, with blank names, invalid Unicode and control characters rejected.

This edits the running session without advancing simulation frames. Save the game to retain the changes in a game snapshot. Naming is available in an eligible field or world session, and is disabled during active battles, title/movie sessions and faulted fields.

## Weapons & Limit Breaks

Choose **Battle / Weapons & Limit Breaks** to edit the current party. Expand a weapon type to see its individual weapons. Green marks the character’s equipped type and weapon. **Base** creates and equips a new weapon of that type at its starting grade.

Another character’s portrait beside a weapon means they have it equipped. Selecting a weapon owned by someone else opens an exchange. The arrows show which weapon each character gives, and the list under each name shows which Limit Breaks that character will lose access to. **Yes** stages the exchange. **No** returns to the same weapon.

In the Limit Break list, each portrait identifies the family’s original character. Toggle a checkbox to change the assignment. Green means the equipped weapon supports an assigned family. Amber means the family stays assigned and becomes available again with a compatible weapon.

Back collapses an open weapon type. Leaving the editor prepares the selected equipment and applies the staged changes for all characters. Save normally to retain them. Changes require an active FFVIII game with no battle, field transition, scripted party change or tutorial in progress. At Title, use the isolated laboratory.

The laboratory's **Equipment and abilities** group has its own weapon and family controls. Installed character prototypes initialize native statistics, and weapon changes recompute equipment contributions while retaining the recipient's own stat inputs. Laboratory changes and explicit presets remain separate from the current game.

## Battle settings and laboratory

The Battle section shows whether actions target a **Game battle** or **Laboratory battle**. **Win current battle** and **Lose current battle** require that displayed battle to be active. **Encounter execution** selects Full combat or Automatic victory for the current game (the new-game default at Title). Choosing Automatic victory can finish an active or pending encounter and requires confirmation of that immediate consequence. Merely opening the page changes nothing. Save the game to retain session rules.

**Additional enemies** stores a value from 0 to 10, with 0 as the default. The gold marker shows the effective request for eligible random encounters. With more than three allies, the request is at least the ally count minus two. Hardcore requests at least one. The encounter, area and roster cap can reduce how many enemies are added. Changes apply to later encounters. At Title, the slider sets the new-game default.

Opening the laboratory initializes its editor from the current party and a recent eligible opponent. If there is no eligible encounter, it selects an enemy from the installed catalog. At the title, party defaults come from New Game data. Laboratory edits remain separate from the running game's state.

Use Scenario, Participants and Presets to keep the editor compact. Select an actor to edit Identity, Vitals and stats, Equipment and abilities, or Status effects. Status controls set the actor's starting conditions. Preset Save and Load are explicit actions. Loading a preset updates the editor and does not start a battle. The three preset slots use `battle-presets.json`, separate from ordinary game snapshots.

The laboratory exposes implemented battle behavior. It does not establish full original combat parity or support for every original statistic and ability.

## Previews and automation

Starting a new automated game or replaying a saved reproduction replaces the running session and requires confirmation when a live game exists. Pause/Resume and Stop retain their existing run behavior.

[Automatic exploration](automatic-exploration.md) uses the same controller in Debug Room and in headless mode. Its coverage and failure snapshots belong to that run.

Developers can render registered UI screens without opening a graphics window:

```powershell
dotnet run --project src/Memoria.FF8.App -c Release -- --menu-preview --screen debug-room --output ./preview/debug-room.png
```

See the [command-line reference](../reference/command-line.md) for available preview screens and the [UI framework](../developers/ui-framework.md) for canvas and layout APIs. CPU previews render fixtures or supplied snapshots to check layout and presentation. Gameplay and GPU rendering need separate checks.
