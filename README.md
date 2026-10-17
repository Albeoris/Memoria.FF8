# Memoria FF8

## Legal Notice and Rights-Holder Information

This is an unofficial, non-commercial, independently developed game engine compatible with FINAL FANTASY VIII. It is a fan-led software project, provided free of charge to support interoperability, preservation, and continued use of legally acquired copies of the game. It is not affiliated with, sponsored by, endorsed by, or approved by Square Enix or any other rights holder.

### Independent implementation

The engine is written from scratch in C#, using its own implementation to reproduce the original engine's behavior and read compatible game data. It is not a recompilation of the original executable or a translation of the original game's source or machine code. The implementation does not incorporate copied original engine code.

### Original game required; no game content distributed

This repository and its official releases do not include or redistribute original game executables, proprietary engine code, or game assets, including data archives, scripts, dialogue, maps, textures, models, fonts, music, sound effects, and video. The engine reads the necessary game content from files supplied locally by the user.

To play FINAL FANTASY VIII with this engine, users must own a legitimate copy of a supported edition and supply the required data from that copy. Copies obtained through Steam, GOG, or original physical media may be used where the corresponding edition is supported. The project does not provide the game itself and is not a substitute for acquiring it legally.

The project does not provide unauthorized game downloads, cracks, or activation keys. Nothing in this notice grants permission to redistribute game content or circumvent DRM or other access controls.

### Ownership and licensing

All rights in FINAL FANTASY VIII, its content, names, characters, artwork, and associated trademarks remain with their respective rights holders. References to the game and storefronts identify compatibility and acquisition sources only; they do not imply affiliation or endorsement.

The project's software license covers only material its contributors are entitled to license. Third-party components remain subject to their respective licenses. No license to the original game, its content, or its trademarks is granted. The description of this project as non-commercial states the maintainers' purpose and distribution model; it does not add restrictions to the applicable software licenses.

### Rights-holder contact

Rights holders or their authorized representatives with a specific concern are invited to contact **albeoris@gmail.com**. Please identify the material in question, its location in the repository or a release, the rights concerned, and your authority to act.

We will review concerns promptly and in good faith and address confirmed issues appropriately. Where practical, please contact us before submitting a takedown request so that misunderstandings or isolated issues can be resolved without unnecessarily affecting independently developed code. This is a voluntary request and does not limit any rights or remedies.

## About

Memoria FF8 is an experimental, standalone Windows recreation of FINAL FANTASY VIII. It reads resources from your own installed copy of the original PC game. Game data, movies, music and saved games are not included.

## Run

Download the Windows archive from this repository's Releases, extract it, and open `Memoria.Launcher.exe`. Keep the adjacent `Data` directory intact. The launcher finds installed copies, lets you choose a game folder, installs required FFmpeg tools on request and starts the engine. Only the English Steam 2013 edition is currently playable; discovery of another edition does not establish support. Experimental [Remastered resource loading](docs/remastered-resources.md) is available through a separate provider and optional test project. Windows 10 or later on x64 hardware is required; the bundled x86 audio process handles original DirectMusic playback. A separate .NET installation is unnecessary for the published application.

This is a work in progress. Some gameplay, battle behavior and rendering remain incomplete. It is an independent project and is not affiliated with Square Enix. FINAL FANTASY and the original game's assets belong to their respective owners.

## Build

Use Windows, PowerShell 7 and the .NET 10 SDK:

```powershell
./Build.ps1
```

The script restores public NuGet packages and builds the self-contained x64 launcher, x64 engine and x86 audio host. It produces `artifacts/Memoria-FF8-win-x64.zip`. The archive has exactly two entries at its root:

```text
Memoria.Launcher.exe
Data/
```

To also run the resource-independent launcher, UI and runtime tests:

```powershell
./Build.ps1 -Test
```

Open `Memoria.FF8.slnx` for development. Individual test executables support the xUnit command line through `dotnet run --project tests/<project>`. Tests that use original resources require `MEMORIA_FF8_GAME_DIR`. Optional saved-state fixtures use `MEMORIA_FF8_TEST_FIXTURE_CATALOG`, a JSON object mapping the resource IDs in the tests to external file paths. Relative catalog paths resolve against `MEMORIA_FF8_TEST_FIXTURES`, or the catalog's directory when that variable is absent. Tests skip when their optional resources are unconfigured. Generated test artifacts use temporary storage. External acceptance checks require a recording workspace through `MEMORIA_FF8_RECOMP_WORKSPACE` and the `recomp` tool (or its executable path in `RECOMP`).

## Automatic exploration

Open Debug Room (F2) → Automatic play → Start a new game, or run the engine without a window:

```powershell
dotnet run --project src/Memoria.FF8.App -c Release -- --auto-play --game-dir "$env:MEMORIA_FF8_GAME_DIR" --output ./autoplay-runs
```

The same controller runs both modes. It starts with native cold-process random seed 1, skips movie presentation while executing movie callbacks, confirms dialogue immediately, uses automatic battle victories and teleports the controlled player between discovered event coordinates. Scripted movement retains its normal engine owner. The controller runs independently until an error, an exhausted frontier or the ending. Pause, resume, stop and failure replay are available in Debug Room. `--max-steps <count>` bounds a diagnostic headless run and saves its stopping point.

Remote refreshes remember actual inspections, and the selected destination survives intermediate transitions and saved runs. New messages or coverage records do not restart the survey. New interaction paths can reopen rooms whose native predicate inputs changed; replaying an authenticated travel path retains inspections while story, party and dream state are known and unchanged. Field initialization establishes the next baseline before ordinary callbacks, and real changes to story or party still reopen exploration. This finite traversal does not enumerate every combination of persistent flags or random outcomes. Survey records affect priority and give no event or dialogue completion credit.

Field candidates come from active actors, lines, areas and native gateways. Both the destination and its approach must lie in the controlled player's connected, unlocked walkmesh component. Native scripted ladders and other authored transitions connect separate components; teleportation never bypasses them. Native interaction, party and dialogue conditions still determine what can actually execute. Unavailable branches are not forced or marked completed. World entrances come from the connected land component and the original entrance predicates. A deterministic graph explores reachable nonstory frontiers before story-changing events and remembers completed events. Potential story writes set exploration priority; observed story changes separately determine whether a completed transition can be reused, only while its recorded conditions still match. Script analysis follows bounded ordinary and party requests. Explicit opening-region priorities finish known Garden frontiers before Balamb, then Fire Cavern; entrance prerequisites retain the qualification scene and timer setup that teleportation could bypass. Within a phase, known interactions precede equally ordered exits. Dynamic script behavior and unsupported world owners can still stop exploration. This experimental mode does not yet establish complete coverage of the game or every conditional event.

Each event also remembers consumed entry and exit conditions independently of the location graph. Naturally changed party members or supported persistent script flags can reopen a previously attempted event. Passive inspection does not execute scripts or invent missing values. Consumed exit conditions suppress self-repetition and do not establish executed branch coverage.

When actual field initialization proves that no player body exists and no remaining native owner can restore control, the controller can reload its authentic pre-event save and defer that particular entrance approach. This requires complete, stable source and destination predicates and an attempt without choices. The unavailable attempt receives no completion credit; changed persistent conditions reopen it. Other approaches remain eligible. Uncertain scripts, input, scene callbacks or unsupported engine contracts still stop the run for inspection. Required pre-event saves and original resource identities survive cold replay.

A composite route can reach a different native choice under changed downstream conditions. After verifying its executed selection prefix, the controller retires that exact recorded path and explores the actual choice with an ordinary before-choice save. Retirement gives no completion or unavailability credit, and it never substitutes the old selection for the new prompt. The retired path remains in the saved diagnostic history.

Each choice gets a standalone snapshot. Alternatives run individually to the next settled control-return boundary; nested choices do not produce a Cartesian product. Each observed transition retains its actual destination and ordered selections before another alternative is restored. Reusing that route requires matching native prompts and a complete observed selection path; unknown legacy choices are not guessed. After checking alternatives, the controller keeps a continuation that advances the story or exposes more unfinished reachable events, retaining the first continuation on a tie. Pending native requests also guard against starting a second event before the first scene has begun. Scripted screens enumerate the original key-query masks with sampled physical press/release edges. Every minute of running wall time produces a new standalone save and a companion `.run.json` containing traversal and choice coverage. Event boundaries and failures also save checkpoints. Keep the run directories, including referenced choice snapshots. Replaying creates a separate run directory, copies all required unfinished-choice and continuation snapshots into it, and preserves the source files. Finished choices retain coverage without requiring their retired snapshots. The resumed run is independent of the original directory:

```powershell
dotnet run --project src/Memoria.FF8.App -c Release -- --auto-play --game-dir "$env:MEMORIA_FF8_GAME_DIR" --resume ./autoplay-runs/<run>/failure-<step>.run.json
```

Failures return exit code 1 and print the exact reproduction command. Ten seconds without new scene, dialogue, event or branch coverage is a failure, checked between controller steps and native VM operations. Pausing or opening the Debug Room suspends that deadline. The pre-failure save reproduces the failing controller step; the fault save preserves the diagnostic state when it remains serializable. Unit tests cover planning, choice coverage, UI controls and checkpoint handling. Installed-resource smoke tests are enabled by `MEMORIA_FF8_GAME_DIR`. Set `MEMORIA_FF8_AUTOPLAY_ACCEPTANCE=1` to additionally enable the full New Game-to-ending acceptance test; it fails on the first unsupported contract or traversal failure.

The required Albeoris.Frameworks source closure is vendored, with its upstream revision and retained changes documented in [vendor/Albeoris.Frameworks/README.md](vendor/Albeoris.Frameworks/README.md). Building needs no sibling repository or submodule. Package versions are centralized in `Directory.Packages.props`. Ordinary builds use the checked-in compiled title shader; its project README describes shader regeneration.

## Automated builds

Pushes to `main` build, test and replace the prerelease `nightly` download. Tags matching `v*` publish a versioned release. GitHub Actions uses the .NET 10 SDK already installed on its Windows runner.

The source is MIT licensed; see [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY.md).
