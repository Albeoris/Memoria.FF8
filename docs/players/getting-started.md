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

The package uses `Data/media-tools/` beside the engine. Download progress shows received bytes and, when the server supplies a file size, a percentage. Verification, extraction and executable checks have separate stages. Existing working tools are reused without an automatic update.

Response headers and each stalled network read have a 15-second timeout. The complete download has no fixed 15-second limit, so a transfer can take several minutes while data keeps arriving. Setup failures report the reason and manual installation instructions.

For manual setup, obtain the Windows x64 LGPL static archive from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases/latest). Put `ffmpeg.exe` and `ffprobe.exe` from its `bin` directory in `Data/media-tools/`, and retain the archive's license, copyright and documentation files there. No `ffplay.exe` or FFmpeg DLLs are required. Restart the launcher to validate the tools.

Direct engine launches can select another media directory; see the [command line](../reference/command-line.md) and [environment reference](../reference/environment-variables.md). The launcher requires ready media tools before starting a game. A direct engine launch may continue without them, but movie-dependent scenes can then fail to continue.

## Start from source

Follow [Build and test](../developers/building-and-testing.md) to create the same package locally. For a direct development launch, set `MEMORIA_FF8_GAME_DIR` to your installation and run from the source root:

```powershell
dotnet run --project src/Memoria.FF8.App -c Release -- --game-dir "$env:MEMORIA_FF8_GAME_DIR"
```

The direct engine has additional launch and diagnostic modes documented in the [command-line reference](../reference/command-line.md).

## If startup fails

| Symptom | Check |
| --- | --- |
| Missing engine or dependency | Extract the whole archive. Keep `Data/` beside the launcher; do not move the launcher alone. |
| No installation found | Use Browse. Verify the selected folder contains the supported original installation. |
| Media setup fails | Check network and write access. Read the installation error or use manual setup above. |
| Game exits immediately | Read the full error in the launcher, including the engine's exit code. Keep that text when reporting the issue. |
| A scene or feature is unsupported | Record the error and an independently loadable save when available. Do not assume a different resource provider makes the edition playable. |

Memoria is still in development. Current rendering, battle and event support has limitations; a successful launch does not establish complete game coverage. See [controls](controls.md), [saves](saves.md) and [Debug Room](debug-room.md) for everyday use.
