# Saves and standalone snapshots

Memoria saves its current state as standalone JSON. Loading restores that state and its supported engine owners. It does not replay inputs, revisit earlier locations or reconstruct the session from New Game. Installed game resources are still required; a save is not a resource package.

## Player saves

The default player directory is `%LOCALAPPDATA%\Memoria.FF8\player-saves`. It contains a rotating set of quick saves (including the compatible `quick.json`) and 60 manual slots, `slot-01.json` through `slot-60.json`, arranged as two banks of 30.

Default keyboard bindings are:

| Key | Action |
| --- | --- |
| F5 | Quick Save |
| F9 | Quick Load |
| F6 | Save menu |
| F10 | Load menu |

Quick Save keeps nine snapshots by default. Config → Interface → Quick saves sets the installation-wide count (at least one). When full, saving replaces the oldest quick save; reducing the count immediately deletes the oldest excess quick saves. Quick Load opens a picker containing only quick saves, newest first, with the newest selected each time. Up/Down selects a save, PageUp/PageDown changes page, Confirm loads and Cancel closes.

Continue opens the Load menu. Up/Down selects a slot, Left/Right changes bank, and PageUp/PageDown changes page. Each page shows five manual slots; Quick Save is pinned separately in Load. Confirm selects or accepts an overwrite; Cancel backs out. Save/Load cursors are remembered while the application runs. Bindings can be changed in Controls.

Menus show saved time and location. Empty or invalid entries cannot be loaded. Before replacing the running session, the application validates the selected state and its VM/resource continuation. A failed load reports its reason and retains the current session. Saving before starting or loading a game is refused.

These files are Memoria snapshots. They are not original PC save files, and original-game import/export is not implied by the slot labels.

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

Snapshots are validated engine state, not a promise of compatibility with every historical revision. An engine correction may introduce a required owner or stronger consistency checks. Missing information must not be replaced by guessed values at load time.

When a migration is necessary:

1. Preserve the source file.
2. Produce a separately named output with a bounded, explicit transformation.
3. Record the source revision, exact changes and target revision alongside the migration's validation.
4. Load the result under the target engine and perform the smallest meaningful continuation check.

Migration does not mean replaying earlier travel or generating rewards, inventory, random state or battle outcomes from historical inputs. There is no generic reconstruction command or Save/Load recreation key in the current application. Keep a working copy of important saves before changing engine versions.

Developer entry points are [GameState](../../src/Memoria.FF8.Runtime/GameState.cs), [GameSession](../../src/Memoria.FF8.Runtime/GameSession.cs), [PlayerSaveStore](../../src/Memoria.FF8.App/PlayerSaveStore.cs) and [PlayerSaveMenu](../../src/Memoria.FF8.App/PlayerSaveMenu.cs).
