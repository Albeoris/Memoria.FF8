# Command-line reference

Run the engine executable, `Data/Memoria.FF8.exe`, from an extracted distribution. The launcher executable opens its own settings window and does not forward command-line arguments. From a source checkout, use `dotnet run --project src/Memoria.FF8.App -c Release --` followed by the same arguments.

Examples below run from the distribution root. Set `MEMORIA_FF8_GAME_DIR` to your installed original game directory first; see [environment variables](environment-variables.md). Snapshots are engine saves, with automatic detection of compact compressed and older plain JSON formats. Support depends on the saved state and the engine contracts reached during continuation.

```powershell
# Check game directory and initial state without opening a window.
./Data/Memoria.FF8.exe --game-dir "$env:MEMORIA_FF8_GAME_DIR" --validate

# Continue an existing standalone snapshot with an explicit quick-save destination.
./Data/Memoria.FF8.exe --state ./saves/session.json --save-state ./saves/quick.json

# Capture a bounded graphical run.
./Data/Memoria.FF8.exe --state ./saves/session.json --capture-frame ./output/frame.png --capture-after 0 --frames 90 --mute-audio
```

There is currently no `--help` switch. Unknown options or missing required values fail with an error and exit code 1. Successful commands return 0; a preview or validation success is not a gameplay compatibility check. Mode selectors must be the **first argument**. Each mode has its own parser; general options are accepted only where stated below.

## Graphical launch options

Without a mode selector, the engine opens the game window. With no `--state` and the default Title scene, it starts a cold process.

| Option | Meaning / default |
| --- | --- |
| `--game-dir DIRECTORY` | Installed game directory. Overrides `MEMORIA_FF8_GAME_DIR`, then `FF8_GAME_DIRECTORY`. |
| `--achievements-local-only` | Keep achievements local and disable external platform initialization. Also available in the launcher. |
| `--probe-steam-achievements --game-dir DIRECTORY` | Standalone explicit Steam integration check: initialize the bundled x64 SDK, validate product/user, pump callbacks and read the mapped achievement/stat. Performs no achievement/stat writes and creates no local progress. |
| `--state FILE` | Load a standalone snapshot, compact compressed or plain JSON. Its state overrides scene initialization options. |
| `--save-state FILE` | Quick-save snapshot destination; default `%LOCALAPPDATA%/Memoria.FF8/checkpoint.json`. |
| `--settings-dir DIRECTORY` | Redirect engine UI preferences, controls, Triple Triad deck history and save-bank configuration to a selected directory. Overrides `MEMORIA_FF8_SETTINGS_DIRECTORY`; otherwise these use `%LOCALAPPDATA%/Memoria/FF8/`. |
| `--media-tools DIRECTORY` | Directory containing `ffmpeg.exe` and `ffprobe.exe`; overrides `MEMORIA_FF8_MEDIA_TOOLS` for direct engine startup. Default `%LOCALAPPDATA%/Memoria.FF8/media-tools/`, beside `player-saves/` and shared across releases. The launcher honors the same environment variable and default. |
| `--scene NAME` | Initial scene: `Title`, `Field`, `Movie` or `WorldMap`, case insensitive. Scene selection alone does not construct a valid gameplay state. |
| `--field ID_OR_NAME` | Request a field. Numeric IDs and aliases `start0`, `bghoke_1`, `bghoke_2`, `bghoke_3`, `bgroad_6` are accepted. This initializes a field; use a snapshot for exact continuation. |
| `--entry NUMBER` | Only `0` is supported. Nonzero entries fail. |
| `--movie INDEX` | Select Movie scene and movie index. |
| `--movie-time SECONDS` | Initial movie position. Use a snapshot to retain related event state. |
| `--resolution WIDTHxHEIGHT` | Positive initial window dimensions; overrides `WindowWidth` and `WindowHeight` in `%LOCALAPPDATA%/Memoria/FF8/ui-settings.json`. Default `1024x768`. |
| `--world-render-limit PIXELS` | World render longest-side cap, `64..4096`; default `2048`. |
| `--mute-audio` | Disable host audio. Audio is also disabled when `--control-dir` is present or a debugger is attached. |
| `--validate` | Check that the game directory exists, load the requested state and print its scene/field. Does not initialize gameplay, rendering, media tools or installed asset compatibility. |
| `--capture-frame PNG` | Save a graphical frame once the capture delay has elapsed. |
| `--capture-after SECONDS` | Capture delay; default `2`. |
| `--frames COUNT` | Exit when the host update-frame count reaches the supplied value. This is not an event-VM frame budget. |
| `--exit-after SECONDS` | Exit after the host game clock reaches the supplied duration. |
| `--control-dir DIRECTORY` | Enable the local file control queue. |
| `--update-control-dir DIRECTORY` | Launcher update handoff queue. Supports status, save-for-update, cancel-update and quit without enabling diagnostic stepping or disabling audio. |
| `--control-step` | Disable automatic field/world time accumulation, so queue commands drive progression. Requires `--control-dir`. |
| `--battle-test FILE` | Direct diagnostic encounter recipe; requires isolated settings/control/save paths and the normal graphical GPU host. See below. |
| `--debug-room` | Open the debug-room UI on startup. |
| `--ui-screen SCREEN_ID` | Open a registered UI screen on startup; diagnostic use. |

Decimal times use a dot. Explicit path arguments are resolved against the invoking working directory before graphical startup changes to the game directory. Supply a full installed-game path in environment variables to avoid working-directory ambiguity.

## Direct diagnostic battle preparation

`--battle-test` is a graphical launch option. It skips startup navigation, runs the real cold-process and New Game initialization, and constructs the declared installed encounter before allocating exploration. Missing native owners or resources fail explicitly. It does not discover a usable formation, certify a fixture or grant an action to a different character.

Use an authored recipe whose encounter, seed, flags and field-derived inputs have been resolved separately. Required snake_case fields are `encounter`, `seed`, `flags`, `field_battle_mode` and `elapsed_counter`. Music selection also requires `battle_music` and `primary_music_channel` unless the declared native flags keep music; a music path that needs a field identity requires `music_field_word`. Unknown root recipe and driver properties are rejected. See [battle validation preparation](../developers/battle-effects-validation.md) for driver inputs and remaining coverage contracts.

```powershell
$env:MEMORIA_FF8_OUTPUT_DIRECTORY = [IO.Path]::GetFullPath('./output/case')
./Data/Memoria.FF8.exe --battle-test ./input/case.json --settings-dir ./output/case/settings --control-dir ./output/case/control --save-state ./output/case/save/checkpoint.json --resolution 1920x1080
```

The settings, control and save directories must be distinct, non-overlapping and outside the user's global settings directory. Supply `--save-state` explicitly. A selected `MEMORIA_FF8_SETTINGS_DIRECTORY` can supply the settings override. An explicit settings-directory override skips legacy preference import from the save directory. Use fresh directories for each independent process. `--state`, non-Title scene initialization, debug UI startup, `--control-step`, software rendering and `--validate` are incompatible.

An optional `driver` recipe submits the same ordinary semantic `Action` or `Attack` twice through the normal graphical pipeline in one live battle. It checks the requested/executed effect binding, conservative owner drain and at least two further presented frames after each drain. Readiness, action and total timeouts are finite. Capability borrowing, timed limits, combat repairs and terminal replay are unsupported and produce blockers. Existing persistent tasks may also block the conservative drain check.

Driver completion is `CANDIDATE_COMPLETED` with `PREPARATION_ONLY` coverage. It produces no accepted performance or effect verdict: `candidate.json` remains `ERROR` while mandatory telemetry is unavailable. The driver exits after completion or a blocker. Without a driver, the diagnostic host remains available through the [control queue](control-api.md#battle-commands); isolated closure uses `battle-close`. Diagnostic lifecycle snapshots cannot be saved for later continuation.

## Original PC save transfer

```powershell
./Data/Memoria.FF8.exe --import-save ./original/slot1_save01.ff8 --output ./saves/imported.json --game-dir "$env:MEMORIA_FF8_GAME_DIR"
./Data/Memoria.FF8.exe --export-save ./saves/imported.json --output ./original/slot1_save01-exported.ff8
```

The mode selector must be first and the source file must follow it immediately. Transfer runs without a game window. These modes accept only:

| Option | Meaning |
| --- | --- |
| `--output FILE` | Required new destination file. Existing destinations and the source path are refused. |
| `--game-dir DIRECTORY` | Import only: installed game resources used to initialize and validate the imported state. Defaults to `MEMORIA_FF8_GAME_DIR`, then `FF8_GAME_DIRECTORY`. |

Import accepts one original PC save, validates its standalone engine continuation, and writes a compact snapshot. Export transfers persistent progress and the saved field or world location. Imported games preserve original bytes without a supported engine owner; games started in Memoria use defaults for original-only properties. Missing process state and properties that cannot transfer receive defaults. Exact movie, battle and event processing is not transferred; the original game initializes the saved location normally. Malformed data or unsupported gameplay continuation fails with exit code 1 and leaves the source and existing output untouched. See [save transfer](../players/saves.md#original-pc-save-transfer) for preservation and compatibility limits.

## Automatic exploration

```powershell
./Data/Memoria.FF8.exe --auto-play --output ./output/exploration --max-steps 1000
./Data/Memoria.FF8.exe --auto-play --resume ./output/exploration/branch/run.json --max-steps 2000
```

`--auto-play` runs the autonomous exploration controller without a graphical window. It accepts general launch options and adds:

| Option | Meaning |
| --- | --- |
| `--output DIRECTORY` | Exploration output root. Defaults to `autoplay` beside the configured quick-save path for a new run. |
| `--resume FILE` | Resume a controller `run.json`, including its snapshot and coverage. Use the resume path printed by the controller. |
| `--max-steps COUNT` | Positive total step limit. On resume this is an absolute controller step limit, not an additional step allowance. |

`--state` is rejected; use `--resume` to preserve exploration state. A new run starts from a cold process. Exploration can stop at unsupported contracts; its availability does not imply a complete playthrough. Presentation, screenshot and graphical timing options do not turn this mode into a windowed host. See [automatic exploration](../players/automatic-exploration.md) for the workflow and output layout.

## Headless actions and control

`--headless-play` applies ordinary player actions or a movement guide to a standalone Field, WorldMap or Movie snapshot. It accepts general launch options and requires `--state`, `--output` pointing to a **new directory**, and exactly one input source:

| Input source | Format |
| --- | --- |
| `--actions FILE` | JSON array of `PlayerAction` objects. |
| `--world-target X,RAW_Z` | One world target in native coordinates. |
| `--world-path FILE` | Nonempty JSON array of ordered `[X, rawZ]` integer pairs. X range `-131072..131071`, raw Z range `-98304..98303`. |
| `--field-path FILE` | Nonempty JSON array of ordered `[X, Y]` finite signed-WORD coordinate pairs. |

`--world-budget COUNT` limits guided movement to `1..30000` actions, default `600`. `--world-tolerance DISTANCE` sets the guide arrival tolerance to `8..64`, default `64`; despite their names, both options also apply to a field path. Guides stop at relevant control, scene or dialogue boundaries; they do not teleport the player.

```powershell
./Data/Memoria.FF8.exe --headless-play --state ./saves/session.json --actions ./input/actions.json --output ./output/action-run
```

The output directory receives `checkpoint.json`, `report.json`, and a `frame.png` when rendering succeeds. `preaction-checkpoint.json` retains the most recent attempted action's starting state. Inspect the report and exit code if execution or rendering stops at an unsupported contract.

For example, an action file can contain a short ordinary wait:

```json
[
  { "Kind": "Wait", "Frames": 1 }
]
```

`--headless-control` instead keeps a local queue host alive. It accepts general launch options and requires `--state` and `--control-dir`, with a Field, WorldMap or Movie snapshot:

```powershell
./Data/Memoria.FF8.exe --headless-control --state ./saves/session.json --control-dir ./output/control
```

This host advances through queue commands rather than a timed simulation loop. It has no raster capture output. General graphical options such as `--capture-frame`, `--frames` and `--exit-after` do not control its lifetime; send `quit` through the queue. Both headless modes protect the player-save directory from diagnostic output writes. See the [control API](control-api.md) for queue requests, actions and results.

## Menu preview

`--menu-preview` renders a shared menu view to PNG without a graphics device. It uses only these options:

| Option | Meaning |
| --- | --- |
| `--output PNG` | Required destination. |
| `--screen SCREEN_ID` | Default `debug-room`; examples include `main`, `party-selection`, `battle-debug`, `name-input`, `native-tutorial`, `triple-triad`. |
| `--game-dir DIRECTORY` | Installed game directory, otherwise the configured environment value. |
| `--state JSON` | Use a standalone snapshot where supported; otherwise preview data is a fixture. `native-tutorial` and `triple-triad` require a snapshot containing the corresponding active interface. |
| `--ui vector\|classic` | Presentation; default `vector`. |
| `--resolution WIDTHxHEIGHT` | Default `960x540` for Vector, `960x672` for Classic; width `320..4096`, height `240..4096`. |
| `--character MENU_ID` | Select a menu character. |
| `--text-scale PERCENT` | Text scale for menu-controller, player-save and party-selection screens: `100`, `125` or `150`; default `100`. |
| `--font-size 6..48` | UI font size for menu-controller, player-save and party-selection screens; default `24`. |
| `--time SECONDS` | Capture text overflow animation after `0..86400` seconds; default `0`. |
| `--expanded` | Expand supported fixture views. |
| `--name-subject NUMBER` | Naming subject; default `0`. |
| `--name-page NUMBER` | Naming page advancement count; default `0`. |
| `--inputs INPUTS` | Comma-separated menu inputs. The `party-selection` preview supports directional inputs; `--expanded` selects a source cell in its fixture. A supplied `--state` displays the retained party panes and pending selection. |

```powershell
./Data/Memoria.FF8.exe --menu-preview --screen main --output ./output/main-menu.png
```

## Battle preview

`--battle-preview` renders battle assets and the shared HUD to PNG without a graphics device. Supply exactly one of `--scenario JSON` (a `BattlePreviewScenario`) or `--snapshot JSON` (a `BattleSnapshot`), plus required `--output PNG`. A battle snapshot is not a whole-game `GameState` snapshot.

Optional arguments: `--game-dir DIRECTORY`, `--resolution WIDTHxHEIGHT` (default `960x672`, width `320..4096`, height `240..4096`), `--frames 0..30000` (default `0`), `--advance-turn` and `--select-target`. The turn advance happens before frame ticks; target selection happens before scenario HUD inputs. Output must differ from source JSON.

```powershell
./Data/Memoria.FF8.exe --battle-preview --snapshot ./input/battle.json --output ./output/battle.png --frames 1
```

## Triple Triad practice

`--triple-triad` starts an isolated practice match. Without `--preview` or `--autoplay`, it opens a practice window. It uses only these options:

| Option | Meaning |
| --- | --- |
| `--game-dir DIRECTORY` | Installed game directory, otherwise the configured environment value. |
| `--save JSON` | Practice save destination; default `%LOCALAPPDATA%/Memoria.FF8/triple-triad-practice.json`. |
| `--state JSON` | Read an available card collection from a story snapshot; story state is not written. |
| `--match JSON` | Restore a practice save. Cannot be combined with `--state` or `--player`. |
| `--player IDS` | Five comma-separated card IDs, each `0..109`, available in the starting collection. |
| `--opponent IDS` | Five comma-separated card IDs, each `0..109`; default `0,1,2,3,4`. |
| `--rules FLAGS` | Case-insensitive rules: `None`, `Open`, `Same`, `Plus`, `SameWall`, `Elemental`, `SuddenDeath`, `Random`; combine with commas and quote the argument. Default `Open`. |
| `--autoplay` | Automatically finish the practice match and save. |
| `--preview PNG` | Render a `960x672` practice view, save the match and exit. Can be combined with `--autoplay`. |

Without a story source, the practice collection starts with one of each card. Omit `--player` for `Random` rules. Practice JSON, preview PNG and source story snapshot must use separate paths.

```powershell
./Data/Memoria.FF8.exe --triple-triad --rules "Open,Same" --save ./saves/practice.json --preview ./output/practice.png
```

Parser sources: [entry point](../../src/Memoria.FF8.App/Program.cs), [general launch options](../../src/Memoria.FF8.Runtime/LaunchOptions.cs), [automatic exploration](../../src/Memoria.FF8.App/AutoPlayCommand.cs), [headless actions](../../src/Memoria.FF8.App/HeadlessPlayer.cs), [headless control](../../src/Memoria.FF8.App/HeadlessControlHost.cs), [menu preview](../../src/Memoria.FF8.App/MenuPreview.cs), [battle preview](../../src/Memoria.FF8.App/BattlePreview.cs) and [practice match](../../src/Memoria.FF8.App/TripleTriadPractice.cs).
