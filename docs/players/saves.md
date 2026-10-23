# Saves and standalone snapshots

Memoria saves its current state as a standalone snapshot. Player saves use a compact compressed format and retain their `.json` filenames; the loader detects the format and also reads older plain JSON snapshots. Loading restores that state and its supported engine owners. It does not replay inputs, revisit earlier locations or reconstruct the session from New Game. Installed game resources are still required; a save is not a resource package.

Difficulty is stored in each Memoria save and standalone battle snapshot. Saves without a difficulty setting load as **Normal**. **Hard** requires a Memoria destination because original FF8 save files cannot retain this choice.

## Player saves

The default player directory is `%LOCALAPPDATA%\Memoria.FF8\player-saves`. It contains a rotating set of quick saves (including the compatible `quick.json`) and one Memoria bank of 100 manual slots, `slot-01.json` through `slot-100.json`. Existing slots from both former Memoria banks appear in this bank with their filenames and contents preserved.

Default keyboard bindings are:

| Key | Action |
| --- | --- |
| F5 | Quick Save |
| F9 | Quick Load |
| F6 | Save menu |
| F10 | Load menu |

Quick Save keeps nine snapshots by default. **Config → Gameplay → Exploration and saving → Quick saves to keep** edits the shared retention limit (at least one). Adjusting the draft leaves files unchanged. Choose Save to review the exact number of older quick saves that would be removed, then confirm deletion. Cancel preserves both the saved limit and files. Increasing the limit, or reducing it without removing files, needs no deletion confirmation. The accepted limit is saved before cleanup; a cleanup failure reports what was removed and what remains while the new limit stays saved. The next quick save applies that limit. Manual slots and explicit checkpoints are preserved. Quick Load opens a picker containing only quick saves, newest first, with the newest selected each time. Up/Down selects a save, PageUp/PageDown changes page, Confirm loads and Cancel closes.

Continue opens the Load menu. The plaques at the top select Memoria (the title-screen lion), Original I and Original II. Memoria holds 100 slots; each original bank holds 30. Up/Down selects a slot, Left/Right changes bank, and PageUp/PageDown changes page. Plaques and slots also accept mouse selection. Each page shows five saves. The Memoria Load bank mixes every quick save with manual saves, newest first, followed by empty manual slots. Quick Load uses the same browser with only the Memoria tab and quick saves. Confirm selects or accepts an overwrite; Cancel backs out. Save/Load cursors are remembered for each bank while the application runs. Bindings can be changed in Controls.

Menus show play time, location and the system date and time of saving. New Memoria saves keep the local clock time and UTC offset recorded when saved, so their displayed time stays the same after a time-zone change. Older snapshots and original PC saves fall back to the file’s local modification time. Empty or invalid entries cannot be loaded. Before replacing the running session, the application validates the selected state and its VM/resource continuation. A failed load reports its reason and retains the current session. Saving before starting or loading a game is refused.

The original banks read and write `slot1_save01.ff8` through `slot1_save30.ff8` and `slot2_save01.ff8` through `slot2_save30.ff8` in the original game's save folder. A unique Steam user folder under Documents is detected automatically. If none or several exist, selecting an original bank opens a folder picker; choose the folder containing the `.ff8` files. The selected folder is remembered for this user.

## Original PC save transfer

In either the Load or Save menu, select an occupied slot and click **Copy to original** or **Copy to Memoria**, or press **Card / detail** (default **S**, Xbox **X**, PlayStation **Square**). The menu switches to the other save format. Select the destination bank and slot, then Confirm. An occupied destination requires a second confirmation. Cancel backs out of overwrite confirmation, then cancels the transfer and returns to the source. The source is preserved. Saving a running game directly into an original bank and loading from either format also use these bank tabs.

Original saves initialize unsupported or absent Memoria state with defaults. Export retains supported persistent progress and uses defaults for properties the original format cannot represent. Exact movie, battle or event processing cannot transfer. Original banks operate on local files; Steam cloud synchronization is managed by Steam and the original game.

The command-line converters remain available. Run these commands from an extracted release directory:

```powershell
./Data/Memoria.FF8.exe --import-save ./original/slot1_save01.ff8 --output ./saves/imported.json --game-dir "$env:MEMORIA_FF8_GAME_DIR"
./Data/Memoria.FF8.exe --state ./saves/imported.json
./Data/Memoria.FF8.exe --export-save ./saves/imported.json --output ./original/slot1_save01-exported.ff8
```

Import reads one original PC `.ff8` save, restores its progress and saved location using the installed resources, and validates the resulting engine continuation before writing a compact standalone snapshot. Properties absent from the original format and transient process state are initialized with defaults. For example, resident music buffers and pending playback are not transferred. `--game-dir` may be omitted when `MEMORIA_FF8_GAME_DIR` or `FF8_GAME_DIRECTORY` is set. Malformed save data or unsupported gameplay continuation fails with an error and no output file.

Export writes one original PC save with the snapshot's persistent progress and a saved field or world location. For an imported game, original bytes without a supported Memoria owner are retained from its source file. A game started in Memoria uses defaults for original-only properties. Properties that cannot be transferred receive defaults; an active movie, battle or event is not resumed at its exact processing point. The original game loads the saved location through its normal initialization.

Both commands require `--output` with a new filename. They refuse an existing destination or the source path, preserve the source file, and return exit code 1 on failure. Keep a backup and copy an exported file into the original game's save directory only when ready to use it.

## Explicit snapshot files

Use `--state <save.json>` to start from a snapshot. `--save-state <path>` selects the explicit checkpoint destination used by diagnostic saves; its default is `%LOCALAPPDATA%\Memoria.FF8\checkpoint.json`. The [control queue](../reference/control-api.md) provides `save` for capturing a running session. Explicit checkpoint writes cannot target the player-saves directory.

From an extracted release directory:

```powershell
./Data/Memoria.FF8.exe --game-dir "$env:MEMORIA_FF8_GAME_DIR" --state ./saves/before-event.json --save-state ./saves/after-event.json
```

The command selects startup and checkpoint paths; it does not automatically write the destination on exit. Use a supported save operation. See [command-line options](../reference/command-line.md) for diagnostic hosts and their save/output controls.

The host captures current scene/VM owners before serialization. `GameSession.Save` writes `<path>.tmp` and atomically replaces the destination. A diagnostic save can preserve a fault for inspection; that does not make the faulted state a safe gameplay resume point.

Keep distinct filenames for milestones. Prefer a stable point before or after an event. Exact mid-event snapshots are useful diagnostics, but continuation is supported only when all active state owners can be captured and restored. Movie snapshots also require the corresponding installed media and a supported playback host.

## Compatibility and migrations

When loading a snapshot outside an executing battle, properties absent from the save are filled recursively from the initial process state also used by New Game. This includes newly added guardian process blocks. Recorded values, including explicit nulls, zeroes, false flags and complete arrays, are preserved. Loading does not rewrite the source file; the next save records the completed state. New saves explicitly retain null properties so they remain distinct from missing properties.

Initial process values do not reset the current field or story progress. A running battle, an incomplete battle snapshot or an effect process with an executed entry must retain its recorded execution state; missing live counters are not synthesized. Existing validation still checks saved types, resource ownership, consistency and compressed-file checksums. Snapshots therefore remain validated engine state rather than a promise of compatibility with every historical revision. Changes that require information beyond known initial values may still need a migration.

When a migration is necessary:

1. Preserve the source file.
2. Produce a separately named output with a bounded, explicit transformation.
3. Record the source revision, exact changes and target revision alongside the migration's validation.
4. Load the result under the target engine and perform the smallest meaningful continuation check.

Migration does not mean replaying earlier travel or generating rewards, inventory, random state or battle outcomes from historical inputs. There is no generic reconstruction command or Save/Load recreation key in the current application. Keep a working copy of important saves before changing engine versions.

Developer entry points are [GameState](../../src/Memoria.FF8.Runtime/GameState.cs), [GameSession](../../src/Memoria.FF8.Runtime/GameSession.cs), [PlayerSaveStore](../../src/Memoria.FF8.App/PlayerSaveStore.cs) and [PlayerSaveMenu](../../src/Memoria.FF8.App/PlayerSaveMenu.cs).

## Card Mod once

**Card Mod once** defaults to **Off**. Enable it before **Begin** in New Game, or under **Config → Gameplay → Exploration and saving**. Each save keeps its own setting; Title changes the default for future games.

When enabled, Card Mod produces one normal recipe batch per unique card ID during the entire game and keeps the cards. You still need the learned ability, the recipe's normal card quantity and room for the complete item result. Extra copies do not grant extra conversions.

Successful conversions are recorded permanently in that save, including conversions while the option is Off. Disabling and re-enabling the option, losing a card or acquiring it again never clears its history. While Off, ordinary Card Mod consumes cards and may repeat as usual. A new game starts with an empty history. Existing saves without this history start empty; past conversions cannot be reconstructed.

The enabled option or any recorded conversion requires a Memoria save slot. Original FF8 save files cannot preserve this history, so exporting to them is refused even after the option is disabled.
