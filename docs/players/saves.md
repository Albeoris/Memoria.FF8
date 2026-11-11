# Saves and standalone snapshots

Memoria player saves retain progress and the logical state needed to continue, using a compact compressed format with `.json` filenames. Graphics resources are loaded from the selected installation and mods. The loader also reads older compressed execution snapshots and plain JSON snapshots. Loading does not replay inputs, revisit earlier locations or reconstruct the session from New Game. Installed game resources are still required; a save is not a resource package.

Quick Save supports ordinary player-controlled exploration and issued field dialogues or choices. Color changes, animation, scrolling backgrounds, music transitions and pending sound loading do not by themselves prevent saving. Actual location transitions, active battles, movies and unsupported gameplay transactions still require a supported boundary. Older battle execution snapshots retain their reader.

Compatible music and ambient-loop playback positions are restored when available. Changing the resource or using a backend that cannot seek drops the position and keeps the selected track. One-shot sounds and audio device buffers are not saved.

Older snapshot schemas are upgraded in memory before loading; the source file stays unchanged. Saving afterward writes the current schema. Migration runs only forward. A save from a newer application may require an application update. Corrupt data or an unsupported required running state still reports a specific loading boundary.

Older compact gameplay saves remain loadable when presentation or process fields are missing. Music is recovered from saved selections where available; absent bar, shake and salary-popup details use documented defaults. A compatibility warning can identify presentation that could not be restored exactly. These defaults preserve saved progress, inventory, rewards, random state and script positions without replaying completed scripts. Missing new fields or ownership markers alone do not require a standalone snapshot or manual migration.

Difficulty is stored in each Memoria save and standalone battle snapshot. Saves without a difficulty setting load as **Normal**. **Hard** and **Hardcore** are retained by Memoria saves. Exporting to an original bank discards the difficulty choice; the exported copy uses the original game's rules.

## FFVII and FFVIII worlds

The active game selects its own player save bank. F5/F6 and F9/F10 have the same actions in both games. FFVII slots and quick saves live in the `ff7` subdirectory of the player save directory; FFVIII retains its existing directory. Each browser shows only its own game. Loading stays in the current game. Use **Switch world** or **Return** explicitly to cross between games.

In FFVII fields, use F5 for a Memoria quick save or F6 to select a Memoria slot, including during the opening before the first original save point. Direct saves to original slots and world switching still require the game's save permission.

In FFVII, the two Original tabs cover native PC banks `save00.ff7` through `save09.ff7`, with 15 slots per file. Choose an original save folder, then use **Transfer** (the configured Card action, S by default) to copy between Memoria and original banks. Import and export currently support native field saves. Native world-map saves can be copied between original slots but cannot yet be imported or exported as Memoria continuations. Native conversion restarts field initialization and omits exact VM positions, dormant FFVIII state and appearance mappings; keep a Memoria slot for those. This supports classic PC bank files; cloud account metadata and other edition formats are outside this conversion.

Recognized FFVII saves in the former shared directory remain readable from the FFVII browser. New FFVII manual saves use its own subdirectory and leave the old manual files unchanged. Quick-save rotation and retention apply only to the current game's quick saves.

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

The original banks read and write `slot1_save01.ff8` through `slot1_save30.ff8` and `slot2_save01.ff8` through `slot2_save30.ff8` in the original game's save folder. Both PC editions use this native payload. Discovery follows the selected installation: Classic uses `Documents/Square Enix/FINAL FANTASY VIII Steam/user_*`; Remastered uses an existing `Documents/My Games/FINAL FANTASY VIII Remastered/Steam/<profile>/game_data/user/saves` or corresponding `GOG/<profile>` folder. A unique profile is selected automatically. If none or several exist, selecting an original bank opens a folder picker; choose the folder containing the `.ff8` files. An explicitly selected folder remains authoritative and is remembered for this user.

## Original PC save transfer

In either the Load or Save menu, select an occupied slot and click **Copy to original** or **Copy to Memoria**, or press **Card / detail** (default **S**, Xbox **X**, PlayStation **Square**). The menu switches to the other save format. Select the destination bank and slot, then Confirm. An occupied destination requires a second confirmation. Cancel backs out of overwrite confirmation, then cancels the transfer and returns to the source. The source is preserved. Saving a running game directly into an original bank and loading from either format also use these bank tabs.

Original saves initialize unsupported or absent Memoria state with defaults. Export retains supported persistent progress and discards Memoria gameplay rules, tuning, pending settings and feature usage histories. Shared magic is moved into native personal banks, preserving Junction ownership and stock quantities within the original limit of 32 spells per character. Export fails if the stock cannot fit or casts are still reserved. GF configuration changes require review of their conversion losses. The source snapshot and running session retain their full configuration. Exact movie, battle or event processing cannot transfer. Original banks operate on local files; Steam cloud synchronization is managed by Steam and the original game.

The command-line converters remain available. Run these commands from an extracted release directory:

```powershell
./Data/Memoria.FF8.exe --import-save ./original/slot1_save01.ff8 --output ./saves/imported.json --game-dir "$env:MEMORIA_FF8_GAME_DIR"
./Data/Memoria.FF8.exe --state ./saves/imported.json
./Data/Memoria.FF8.exe --export-save ./saves/imported.json --output ./original/slot1_save01-exported.ff8 --game-dir "$env:MEMORIA_FF8_GAME_DIR"
```

Import reads one original PC `.ff8` save, restores its progress and saved location using the installed resources and selected mods, and validates the resulting engine continuation before writing a compact standalone snapshot. Use `--mods-dir <directory>` to choose the mod root; resume the snapshot with the same resource selection. Properties absent from the original format and transient process state are initialized with defaults. For example, resident music buffers and pending playback are not transferred. `--game-dir` may be omitted when `MEMORIA_FF8_GAME_DIR` or `FF8_GAME_DIRECTORY` is set. Malformed save data, a failed mod resource publication or unsupported gameplay continuation fails with an error and no output file.

Export writes one original PC save with the snapshot's persistent progress and a saved field or world location. Use `--game-dir` to declare the installed original game's initialization and GF definitions. Imported native bytes and explicit saved owners are preserved; missing native initialization bytes use the declared target resources. Memoria gameplay rules and usage histories are discarded in the exported copy. An active movie, battle or event is not resumed at its exact processing point. Unsupported or uncertain state that cannot be represented exactly is rejected. The original game loads the saved location through its normal initialization.

When GF configuration or progression cannot fit the original format, export reports its conversion losses. Review that message before repeating the command with `--allow-gf-loss`; this export-only flag requires an explicit `--game-dir`. It authorizes the reported GF projection and preserves the full Memoria source.

For direct use by Classic Steam, choose the actual `user_...` save profile and an unused native filename such as `slot1_save23.ff8` as `--output`. Memoria signs the final compressed file in that profile's `metadata.xml`, preserving unrelated metadata. If metadata is absent, it creates the complete profile catalog and signs existing native files too. Copying a raw exported file into a Classic Steam profile by hand does not update this required signature; publish directly into the profile instead. Remastered native save profiles receive the `.ff8` file without Classic metadata creation or changes. A raw save directory outside a recognized profile also receives only the `.ff8` file.

Memoria exports refresh the original wrapping save count and bank ordering serial so the original game can identify the newly written save. Native-to-native bank copies preserve the source file bytes exactly. The save and profile metadata are staged and flushed before publication. Invalid metadata leaves the destination unchanged; if metadata replacement fails after the save replacement, the previous save is restored and temporary files are removed.

Both commands require `--output` with a new filename. They refuse an existing destination or the source path, preserve the source file, and return exit code 1 on failure. Use the in-game save browser when replacing an occupied original slot.

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

New Game creates a fresh session from the installed game's initial data. Loading creates a fresh session from the selected snapshot. Neither operation inherits gameplay memory, random generators, timers or pending achievement observations from the previous session. Global display, audio and control preferences remain configured. Saved execution state belongs to the selected snapshot and is restored with it.

When loading a snapshot outside an executing battle, properties absent from the save are filled recursively from the initial process state also used by New Game. This includes newly added guardian process blocks. Recorded values, including explicit nulls, zeroes, false flags and complete arrays, are preserved. Loading does not rewrite the source file; the next save records the completed state. New saves explicitly retain null properties so they remain distinct from missing properties.

Initial process values do not reset the current field or story progress. A running battle, an incomplete battle snapshot or an effect process with an executed entry must retain its recorded execution state; missing live counters are not synthesized. Existing validation still checks saved types, resource ownership, consistency and compressed-file checksums. Snapshots therefore remain validated engine state rather than a promise of compatibility with every historical revision. Changes that require information beyond known initial values may still need a migration.

Schema 3 initializes absent Sigh state and complete legacy uninitialized Sigh placeholders when loading older snapshots. This covers the retained field and battle copies, including saves immediately before Sigh starts. Initialized effects, partial state and current-schema owners retain their saved values.

Schema 4 extends this migration to the other supported legacy effect-source placeholders. It preserves bound sources, modified bytes and active effect tasks, including compact saves and retained field copies. The source file is read without modification; saving normally writes the upgraded state.

Schema 6 initializes missing world texture and palette animation clocks in older saves. Only the visual animation phase restarts from zero; field, story, travel and other process state are preserved. Recorded clocks are retained, and partially recorded clocks remain invalid. Loading upgrades the state in memory without changing the source file; the next save writes the current schema.

Schema 7 restarts entirely absent historical field particle presentation from the field's original resources. It retains saved emitter settings and applies the original warmup from visual random phase zero. Existing particle state is preserved. This resets transient particle appearance without advancing gameplay.

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

## Weapon upgrade restrictions

**Weapon magazine required** and **Weapon upgrade order** default to **Off**. Enable either before **Begin** in New Game or under **Config → Gameplay → Exploration and saving**. The choices are stored in each game; Title sets defaults for future games. Hardcore enables and locks both.

The magazine restriction uses the same saved known-recipe flag that normally makes a weapon visible without enough materials. While enabled, materials alone cannot discover or unlock a recipe. Recipes already discovered before enabling the restriction remain known.

The upgrade-order restriction requires the previous weapon in the character's recipe sequence to have been obtained. This sequence comes from the recipe data. Equipped weapons and successful remodels are remembered even while the restriction is off; changing weapons or toggling the mode does not erase that history. Existing saves without history start with their currently equipped weapons when entering a workshop; earlier ownership cannot be recovered.

Both settings and retained weapon history require a Memoria save. Original FF8 saves cannot preserve them.
