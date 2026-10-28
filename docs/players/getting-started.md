# Installation and first launch

[Documentation](../README.md) / Players

## Requirements

- Windows 10 or later on x64 hardware.
- Your own installed **English Steam 2013** copy of FINAL FANTASY VIII.
- A writable directory for the extracted Memoria package and downloaded media tools.

The release includes the .NET runtimes it needs. Players do not need to install the SDK. Remastered has an [experimental resource provider](../developers/remastered-resources.md); it is not a supported playable edition.

## Install and select a game

1. Download `Memoria-FF8-win-x64.zip` from the repository's [releases](https://github.com/Albeoris/Memoria.FF8/releases). `nightly` contains the latest successful build from `main`; versioned releases are listed separately.
2. Extract the entire archive into a writable directory. Keep this layout:

   ```text
   Memoria.Launcher.exe
   Data/
   ```

3. Open `Memoria.Launcher.exe`.
4. Select an installation from the list, or use **Browse** to choose the game directory manually. Detecting an installation does not establish compatibility with its edition or language.
5. Allow FFmpeg setup when prompted, then press **Play**.

The launcher remembers the selected folder after a successful start and offers it as the last-used installation on later launches. Its selection is stored in `%LOCALAPPDATA%/Memoria/FF8/launcher.json`. Choosing a game directory does not copy its assets into the Memoria distribution.

## FFmpeg setup

FFmpeg and FFprobe provide movie playback and replacement audio decoding. They are external tools with their own licenses. The launcher checks whether both tools run, asks before downloading, verifies the archive against the release SHA-256 manifest, and keeps its license notices alongside the extracted executables.

The launcher and engine use `%LOCALAPPDATA%/Memoria.FF8/media-tools/` by default, beside the `player-saves/` folder. This shared folder survives installing new releases, so existing working tools are reused without another download or an automatic update. To reuse tools from an older release, move its entire `Data/media-tools/` folder, including licenses and notices, to this location.

Download progress shows received bytes and, when the server supplies a file size, a percentage. Verification, extraction and executable checks have separate stages.

Response headers and each stalled network read have a 15-second timeout. The complete download has no fixed 15-second limit, so a transfer can take several minutes while data keeps arriving. Setup failures report the reason and manual installation instructions.

For manual setup, obtain the Windows x64 LGPL static archive from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases/latest). Put `ffmpeg.exe` and `ffprobe.exe` from its `bin` directory in `%LOCALAPPDATA%/Memoria.FF8/media-tools/`, and retain the archive's license, copyright and documentation files there. No `ffplay.exe` or FFmpeg DLLs are required. Restart the launcher to validate the tools.

On macOS, no prebuilt LGPL download exists, so FFmpeg setup asks before building FFmpeg from its official source release. Install the Xcode Command Line Tools when macOS offers them, then start setup again; the build takes several minutes and writes `ffmpeg-build.log` beside the tools if it fails. Homebrew's `ffmpeg` is rejected because it enables GPL components.

Set `MEMORIA_FF8_MEDIA_TOOLS` to select another media directory for both the launcher and engine. Direct engine launches can override it with `--media-tools`; see the [command line](../reference/command-line.md) and [environment reference](../reference/environment-variables.md). The launcher requires ready media tools before starting a game. A direct engine launch may continue without them, but movie-dependent scenes can then fail to continue.

## New Game battle options

**Limit once per turn** is Off by default. When enabled, each character gets one Limit availability check per ATB cycle. Switching allies or reopening the command menu keeps the first result, including a failed check. Another check becomes available after the gauge resets and fills again, such as after acting or being defeated and revived. Saving and loading preserves the result. The option belongs to the saved game and can also be changed under Config → Combat between battles; enabled saves use the Memoria format.

**Selphie card Slots** replaces Selphie’s ordinary Slot rerolls with one choice from 3, 5, 7 or 9 cards, according to her current Crisis Level. Each card independently starts revealed with a 50% chance; hidden cards can also be chosen. Confirm one card to cast its stored spell and count. You cannot cancel or reroll the deal. Enable it before **Begin** in New Game or under **Config → Gameplay → Combat** between battles. It defaults to **Off** on Normal and Hard; Hardcore always enables and locks it. The choice belongs to the save, requires Memoria format when enabled, and applies to subsequent battles.

**Draw once per spell** is Off by default. When enabled, successful Draw Stock or Draw Cast exhausts that spell on that enemy for the whole party until battle ends; a failed Draw does not. Choose it in New Game or change it outside battles under **Config → Gameplay → Magic and Guardian Forces**. The choice belongs to the saved game, enabled saves use Memoria format, and Title edits set the default for future games. See [Magic rules](magic-system.md) for details.

## Start from source

Follow [Build and test](../developers/building-and-testing.md) to create the same package locally. For a direct development launch, set `MEMORIA_FF8_GAME_DIR` to your installation and run from the source root:

```powershell
dotnet run --project src/Memoria.FF8.App -c Release -- --game-dir "$env:MEMORIA_FF8_GAME_DIR"
```

The direct engine has additional launch and diagnostic modes documented in the [command-line reference](../reference/command-line.md).

## Config

Config contains **Graphics, Audio, Controls, Interface, Gameplay and Mods**. Graphics groups the four scene frame limits; Interface groups text/fonts and battle cursor memory. Gameplay separates Combat, Magic and Guardian Forces, and Exploration and saving. Mods provides a searchable package table with independent status and format filters, full-page navigation, and selected-package details. Clicking a row selects it; its checkbox or Confirm changes the saved requested setting immediately. Runtime status separately shows what is currently in use, including failed or blocked changes. Actions opens search, filters, diagnostics and policy controls; Create Mods opens the GF authoring tools.

Presentation, audio, bindings and controller preferences are shared across saves and saved when accepted. Game rules during play belong to the current session: save the game normally to retain them. At Title, supported rule defaults configure future games; Shared magic stock, Infinite magic and Smart Attack are choices for the next New Game. Pending magic changes wait for a safe boundary and may require review. Quick-save retention has a draft and deletion confirmation; see [saves](saves.md).

## Difficulty

Choose **Difficulty** before pressing **Begin** in New Game, or change it later under **Config → Gameplay → Combat**. The choice belongs to the current save; changing it at the title screen sets the default for future games.

- **Normal** preserves reduced enemy magic damage and hidden random idle turns.
- **Hard** removes the enemy magic damage reduction and hidden random idle turns. Explicit scripted do-not-attack actions and state-dependent waits remain.
- **Hardcore** includes the Hard rules and permanently enables all optional New Game rules for that game. These rules and the difficulty selector are locked. ATB mode and rare-card markers remain editable; choosing Hardcore leaves their current choices unchanged. New optional New Game rules are included automatically.

All difficulties use the same initial ATB calculation for party members and enemies, including each participant's current Speed. Status restrictions and equipped initiative abilities still apply. Hard and Hardcore require a Memoria save slot so the choice survives loading.

## If startup fails

| Symptom | Check |
| --- | --- |
| Missing engine or dependency | Extract the whole archive. Keep `Data/` beside the launcher; do not move the launcher alone. |
| No installation found | Use Browse. Verify the selected folder contains the supported original installation. |
| Media setup fails | Check network and write access. Read the installation error or use manual setup above. |
| Game exits unexpectedly | The launcher shows the exit code, recent error output and the saved log path. Keep the log when reporting the issue. |
| A scene or feature is unsupported | Record the error and an independently loadable save when available. Do not assume a different resource provider makes the edition playable. |

Memoria is still in development. Current rendering, battle and event support has limitations; a successful launch does not establish complete game coverage. See [controls](controls.md), [saves](saves.md) and [Debug Room](debug-room.md) for everyday use.

Every launch from the launcher displays console output and complete exception stacks in a separate console window and saves them as they arrive in a separate UTF-8 log under `%LOCALAPPDATA%/Memoria/FF8/logs/`. The console remains available after the game exits while the launcher is open, so error details can be copied. Logs survive game crashes and release updates. If that location cannot be written, the launcher tries `%TEMP%/Memoria.FF8/logs/`; if neither works, the error remains visible in the launcher with a warning that it could not be saved. A process terminated without reporting an error may leave only its last console output and exit code. Direct engine launches use their own console and are not captured by the launcher.

On macOS, install user mods in `~/Library/Application Support/Memoria/FF8/Mods`. Keep them outside `Memoria.FF8.app`; automatic updates replace the complete application bundle. The launcher and direct game starts use this writable mod directory by default. An explicit `--mods-dir` can select a different directory. Windows and Linux keep user mods in the package’s adjacent `Mods` folder.
