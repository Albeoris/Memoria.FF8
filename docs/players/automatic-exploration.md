# Automatic exploration

[Documentation](../README.md) / Players

Automatic exploration is an experimental diagnostic mode. It can stop at unsupported engine behavior and does not establish complete game coverage.

## Start a run

Open Debug Room (F2) → Automatic play → Start a new game, or run the engine without a window:

```powershell
dotnet run --project src/Memoria.FF8.App -c Release -- --auto-play --game-dir "$env:MEMORIA_FF8_GAME_DIR" --output ./autoplay-runs
```

The same controller runs both modes. It starts with native cold-process random seed 1, skips movie presentation while executing movie callbacks, confirms dialogue immediately, uses automatic battle victories and teleports the controlled player between discovered event coordinates. Scripted movement retains its normal engine owner. The controller runs independently until an error, an exhausted frontier or the ending. Pause, resume, stop and failure replay are available in Debug Room. `--max-steps <count>` bounds a diagnostic headless run and saves its stopping point.

## Exploration policy

Remote refreshes remember actual inspections, and the selected destination survives intermediate transitions and saved runs. New messages or coverage records do not restart the survey. New interaction paths can reopen rooms whose native predicate inputs changed; replaying an authenticated travel path retains inspections while story, party and dream state are known and unchanged. Field initialization establishes the next baseline before ordinary callbacks, and real changes to story or party still reopen exploration. This finite traversal does not enumerate every combination of persistent flags or random outcomes. Survey records affect priority and give no event or dialogue completion credit.

Field candidates come from active actors, lines, areas and native gateways. Both the destination and its approach must lie in the controlled player's connected, unlocked walkmesh component. Native scripted ladders and other authored transitions connect separate components; teleportation never bypasses them. Native interaction, party and dialogue conditions still determine what can actually execute. Unavailable branches are not forced or marked completed. World entrances come from the connected land component and the original entrance predicates. A deterministic graph explores reachable nonstory frontiers before story-changing events and remembers completed events. Potential story writes set exploration priority; observed story changes separately determine whether a completed transition can be reused, only while its recorded conditions still match. Script analysis follows bounded ordinary and party requests. Explicit opening-region priorities finish known Garden frontiers before Balamb, then Fire Cavern; entrance prerequisites retain the qualification scene and timer setup that teleportation could bypass. Within a phase, known interactions precede equally ordered exits. Dynamic script behavior and unsupported world owners can still stop exploration. This experimental mode does not yet establish complete coverage of the game or every conditional event.

## Event and choice state

Each event also remembers consumed entry and exit conditions independently of the location graph. Naturally changed party members or supported persistent script flags can reopen a previously attempted event. Passive inspection does not execute scripts or invent missing values. Consumed exit conditions suppress self-repetition and do not establish executed branch coverage.

When actual field initialization proves that no player body exists and no remaining native owner can restore control, the controller can reload its authentic pre-event save and defer that particular entrance approach. This requires complete, stable source and destination predicates and an attempt without choices. The unavailable attempt receives no completion credit; changed persistent conditions reopen it. Other approaches remain eligible. Uncertain scripts, input, scene callbacks or unsupported engine contracts still stop the run for inspection. Required pre-event saves and original resource identities survive cold replay.

A composite route can reach a different native choice under changed downstream conditions. After verifying its executed selection prefix, the controller retires that exact recorded path and explores the actual choice with an ordinary before-choice save. Retirement gives no completion or unavailability credit, and it never substitutes the old selection for the new prompt. The retired path remains in the saved diagnostic history.

## Checkpoints and replay

Each choice gets a standalone snapshot. Alternatives run individually to the next settled control-return boundary; nested choices do not produce a Cartesian product. Each observed transition retains its actual destination and ordered selections before another alternative is restored. Reusing that route requires matching native prompts and a complete observed selection path; unknown legacy choices are not guessed. After checking alternatives, the controller keeps a continuation that advances the story or exposes more unfinished reachable events, retaining the first continuation on a tie. Pending native requests also guard against starting a second event before the first scene has begun. Scripted screens enumerate the original key-query masks with sampled physical press/release edges. Every minute of running wall time produces a new standalone save and a companion `.run.json` containing traversal and choice coverage. Event boundaries and failures also save checkpoints. Keep the run directories, including referenced choice snapshots. Replaying creates a separate run directory, copies all required unfinished-choice and continuation snapshots into it, and preserves the source files. Finished choices retain coverage without requiring their retired snapshots. The resumed run is independent of the original directory:

```powershell
dotnet run --project src/Memoria.FF8.App -c Release -- --auto-play --game-dir "$env:MEMORIA_FF8_GAME_DIR" --resume ./autoplay-runs/<run>/failure-<step>.run.json
```

## Failures and acceptance checks

Failures return exit code 1 and print the exact reproduction command. Ten seconds without new scene, dialogue, event or branch coverage is a failure, checked between controller steps and native VM operations. Pausing or opening the Debug Room suspends that deadline. The pre-failure save reproduces the failing controller step; the fault save preserves the diagnostic state when it remains serializable. Unit tests cover planning, choice coverage, UI controls and checkpoint handling. Installed-resource smoke tests are enabled by `MEMORIA_FF8_GAME_DIR`. Set `MEMORIA_FF8_AUTOPLAY_ACCEPTANCE=1` to additionally enable the full New Game-to-ending acceptance test; it fails on the first unsupported contract or traversal failure.

For every mode and option, see the [command-line reference](../reference/command-line.md). The [control API](../reference/control-api.md) documents external observations and commands. Ordinary standalone game snapshots are described in [Saves](saves.md).
