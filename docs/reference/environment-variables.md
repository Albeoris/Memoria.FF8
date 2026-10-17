# Environment variables

Set these in the shell before starting the engine or test process. An explicit command-line path takes precedence where the corresponding option exists. Use full paths for installed games and externally supplied resources; do not copy game assets into the source checkout.

## Engine startup

| Variable | Purpose and precedence |
| --- | --- |
| `MEMORIA_FF8_GAME_DIR` | Original installed game directory. Used when `--game-dir` is absent. |
| `FF8_GAME_DIRECTORY` | Fallback game directory when `MEMORIA_FF8_GAME_DIR` is unset. |
| `MEMORIA_FF8_MEDIA_TOOLS` | Directory containing `ffmpeg.exe` and `ffprobe.exe`, overridden by `--media-tools`. Otherwise graphical startup uses `media-tools` beside the engine executable. |
| `MEMORIA_FF8_FFMPEG_RELEASE_URL` | Override the HTTPS release-download directory used by optional FFmpeg installation. Default `https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/`. |
| `MEMORIA_FF8_FFMPEG_ARCHIVE` | Override the optional FFmpeg ZIP filename. Default `ffmpeg-master-latest-win64-lgpl.zip`. Must be a filename ending in `-win64-lgpl.zip`. |

For example, from an extracted release directory, with an existing PowerShell variable holding your selected installation:

```powershell
$env:MEMORIA_FF8_GAME_DIR = $selectedGameDirectory
$env:MEMORIA_FF8_MEDIA_TOOLS = (Resolve-Path ./media-tools).Path
./Data/Memoria.FF8.exe --validate
```

The game-directory fallback checks whether a variable is unset, rather than skipping an empty value. Remove an unwanted override with `Remove-Item Env:MEMORIA_FF8_GAME_DIR` instead of assigning an empty string and expecting the fallback to apply. Media-tool resolution treats a blank configured value as the default directory.

FFmpeg release/archive overrides affect the optional installer, not movie selection. Installation downloads the matching checksum list, verifies the archive and validates the executables. A fixed release URL and matching archive filename can pin the download. Keep the FFmpeg license and notices with the installed tools. The graphical host asks before downloading; declining may prevent movie-dependent scenes from continuing.

These settings describe direct engine startup. The graphical launcher selects a game directory through its own settings and passes it explicitly, along with `Data/media-tools` from its package directory. Use direct engine arguments to override these paths. The launcher does not use the game-directory variables as a replacement for its selection UI.

## Optional installed-resource tests

Tests that need installed resources or external fixtures require explicit configuration. These inputs are not part of the public distribution.

| Variable | Purpose |
| --- | --- |
| `MEMORIA_FF8_GAME_DIR` / `FF8_GAME_DIRECTORY` | Original game resources for installed-resource tests. |
| `MEMORIA_FF8_TEST_FIXTURE_CATALOG` | JSON object mapping neutral test resource IDs to externally supplied fixture paths. |
| `MEMORIA_FF8_TEST_FIXTURES` | Fixture base directory. If unset, the catalog's containing directory is used. Relative catalog entries resolve against this base. |
| `MEMORIA_FF8_REMASTERED_DIR` | Optional Remastered installation for installed Remastered tests. It does not select the playable engine's main installation. |
| `MEMORIA_FF8_AUTOPLAY_ACCEPTANCE` | Set to `1` to opt in to the full autonomous exploration acceptance test. This does not enable the CLI mode, which needs no environment flag. |
| `FF8_DEMASTER_PACKAGE` | Optional texture-package root used by Demaster texture tests. |
| `MEMORIA_FF8_JUNCTION_CINEMA_ARCHIVE` | Optional Junction archive override for the resource-host test; otherwise that test checks `Mods/Junction/cinema-rf.iroj` in the configured game directory. |

A minimal external fixture catalog has this form:

```json
{
  "Suite.Test.Resource": "fixtures/example.json"
}
```

Use the exact neutral resource IDs requested by the test being run. An ID is only a lookup key: it does not create, download or validate the contents of a fixture. Fixture-specific tests can have additional opt-in variables beside their declarations. The shared test resource helper skips configured resource-dependent xUnit checks when required inputs are absent; standalone runners and specialized tests may report missing inputs differently.

## External acceptance adapter

The optional acceptance project can invoke an independently configured `recomp` tool and recording workspace:

| Variable | Purpose |
| --- | --- |
| `MEMORIA_FF8_RECOMP_WORKSPACE` | External workspace used by the acceptance adapter; missing/unavailable workspaces cause those tests to skip. |
| `RECOMP` | Executable path/name for the adapter; default `recomp`. |
| `MEMORIA_FF8_CANDIDATE` | Scenario candidate override; otherwise the acceptance assembly's configured `Candidate` metadata is used. |

This adapter is not a runtime or build dependency. Its workspace, recordings and tool must be supplied separately.

## Test diagnostics

Individual test classes also read opt-in variables for regression source snapshots, migration outputs, rendered images and diagnostic exports. They are test hooks, not engine launch settings. Before setting one, inspect its consuming test and choose separate input/output paths. To find the current declarations from the source root:

```powershell
rg -n 'GetEnvironmentVariable' tests src
```

Sources: [launch options](../../src/Memoria.FF8.Runtime/LaunchOptions.cs), [media-tool setup](../../src/Memoria.FF8.App/MediaToolsInstallation.cs), [shared test resources](../../tests/Shared/TestResources.cs), [external acceptance adapter](../../tests/Memoria.FF8.Acceptance/AcceptanceTests.cs), [autoplay acceptance gate](../../tests/Memoria.FF8.App.Tests/AutoPlayAcceptanceTests.cs), [installed Remastered tests](../../tests/Memoria.FF8.Remastered.Tests/InstalledRemasteredTests.cs) and [texture package tests](../../tests/Memoria.FF8.Mods.Tests/DemasterTextureTests.cs).
