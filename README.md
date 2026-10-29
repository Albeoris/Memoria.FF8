<h1 align="center">Memoria FF8</h1>

<p align="center">
  <strong>A from-scratch recreation of the <em>FINAL FANTASY VIII</em> engine in C#, built to bring the original game to life through an independent, readable implementation.</strong>
</p>

<p align="center">
  <a href="#about"><img src="https://img.shields.io/badge/status-experimental-orange" alt="Status: experimental"></a>
  <a href="https://github.com/Albeoris/Memoria.FF8/actions/workflows/build.yml"><img src="https://github.com/Albeoris/Memoria.FF8/actions/workflows/build.yml/badge.svg" alt="Desktop distributions"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue" alt="License: MIT"></a>
  <a href="#play"><img src="https://img.shields.io/badge/playable-Windows_x64-blue" alt="Playable: Windows x64"></a>
</p>

<p align="center">
  <a href="https://github.com/Albeoris/Memoria.FF8/releases">Download</a> ·
  <a href="#about">About</a> ·
  <a href="#build">Build</a> ·
  <a href="#documentation">Documentation</a> ·
  <a href="https://github.com/Albeoris/Memoria.FF8/issues">Report a bug</a>
</p>

<p align="center">
  <a href="https://www.patreon.com/Albeoris/join"><img src="https://img.shields.io/badge/Support_on-Patreon-FF424D?style=for-the-badge" alt="Support on Patreon"></a>
  <a href="https://boosty.to/albeoris"><img src="https://img.shields.io/badge/Support_on-Boosty-F15F2C?style=for-the-badge" alt="Support on Boosty"></a>
</p>

> [!IMPORTANT]
> This project ships **no game files**. You need your own legally obtained copy of a supported edition of *FINAL FANTASY VIII*. Memoria FF8 is an independent fan project and is not affiliated with or endorsed by the game's rights holders.

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

Memoria FF8 is an experimental, standalone Windows engine for FINAL FANTASY VIII. It reads resources from your own installed copy; game data, movies, music and saved games are not included. The supported playable edition is **English Steam 2013 on Windows x64**. Gameplay, battle behavior and rendering remain under development. English Remastered also supports the verified opening route from New Game into the classroom; further gameplay remains experimental and unimplemented contracts stop with an explicit error.

Watch the [gameplay video](https://youtu.be/QnqDSBLkN58).

## Play

Download the Windows archive from [Releases](https://github.com/Albeoris/Memoria.FF8/releases), extract it, and open `Memoria.Launcher.exe`. Keep the adjacent `Data/` directory intact. The launcher discovers installations, offers manual folder selection, sets up FFmpeg with your consent and remembers your last successful selection.

The launcher checks GitHub for the latest versioned release and nightly build at startup. Use **Latest release** or **Nightly** to download and install the selected build, including while the game is running. Installation shows its version and download size before confirmation; switching channels can install an older build. After downloading and verifying the package, the launcher asks whether to restart for installation. Choose **No** to keep playing and retain the package. Selecting the same channel again checks for a newer package and reuses the verified download when unchanged. Internet access is only required for checking and downloading updates, and a failed check does not prevent Play.

If you accept a restart while playing, Memoria asks the game to create one separate save with a unique name under `%LOCALAPPDATA%/Memoria.FF8/auto-update/`. Only after the game confirms that save does the launcher close it, install the update, and automatically start the new version with that save. Ordinary save slots are untouched, and the automatic save remains available afterward. If the game cannot save at the current event boundary or does not answer, the launcher displays the game's reason or the timeout, keeps the download, and asks you to finish and close the game yourself before installing.

Updates verify the archive's GitHub SHA-256 digest and its complete file inventory before installation. A separate copy of the launcher waits for the current process to close, backs up existing distribution files, installs new files, removes obsolete distribution files, and restarts Memoria. Mods, saves, settings and files outside the distribution inventory are preserved; conflicting unowned application files stop installation. Bundled native mod files update only while unchanged from their previous defaults; edited or pre-existing native mod files are preserved. Run the launcher from a writable extracted folder and close any game processes started outside this launcher before updating. No administrator rights are requested.

The distribution inventory is `Data/distribution.json`. Older archives without this inventory must first be extracted into a new folder. Do not unpack an archive over an existing installation: obsolete files would remain. Failed installation restores the previous files; after interruption, startup attempts recovery from `.memoria-update`. Keep that folder if recovery reports an error, close processes using the installation, and start the launcher again. The next update removes the previous completed transaction's temporary files and backups.

Follow [Installation and first launch](docs/players/getting-started.md) for requirements and troubleshooting. The release includes the required .NET runtimes.

## Build

From a source checkout, use Windows, PowerShell 7 and the .NET 10 SDK:

```powershell
./Build.ps1
```

This validates and packages the distribution. Run focused tests for the changed contract separately; see [Build and test](docs/developers/building-and-testing.md). The optional `-Test` switch runs broader checks, including the entire Runtime.Tests project and its battle-effect tests.

The [cross-platform migration](docs/developers/cross-platform.md) adds portable core and self-contained launcher builds for Windows, Linux and macOS. Released macOS packages target Intel; Apple Silicon Macs can build a native `osx-arm64` package locally. The complete playable distribution remains Windows DX11 at this stage. Windows developers can use [one-command Linux testing](docs/developers/linux-testing.md) through WSL2 or a configured SSH hardware target; real Linux GPU acceptance remains separate.

GitHub Actions serializes packaging by commit SHA. Main pushes, version tags and manual reruns reuse the same tested distribution, including when a tag is pushed after its main build finishes. Release and nightly publication both use that ZIP. Failed builds can be retried; a successful build is not repeated after an upload or publication failure.

Distribution artifacts are retained for 90 days. Keep workflow run records: if the original ZIP expires or is deleted, publication fails instead of rebuilding the commit. Use the original ZIP from an existing release for manual publication in that case. NuGet packages are cached between commits; the already compressed distribution ZIP is uploaded without another compression pass.

The result is `artifacts/Memoria-FF8-win-x64.zip`, with this layout:

```text
Memoria.Launcher.exe
Data/
  BundledMods/
    Memoria/
      Depixelation/
Mods/
  Tonberry/
  Demaster/
  Junction/
```

Tonberry, Demaster and Junction directories are empty and ready for user-installed mods. `Data/BundledMods/Memoria/Depixelation` is a bundled native [field mod](docs/modding/field-mods.md); it contains scene configuration and loads all game resources from your installation. Game-directory and selected user mods take priority over bundled files with the same package ID and relative path. Bundled files fill missing paths and use the selected package’s enable setting. Bundled files stay under `Data/` so existing launchers can install future releases.

Choose **Game resource language** in the launcher when a file replacement uses language-specific game resources. **Auto** keeps the installed source's default. The choice applies on the next launch; it changes neither the executable profile nor the interface language. A matching resource must exist in the installation or enabled mods; another language is never used as a fallback. The direct game command accepts `--resource-language en|fr|de|es|it` (or `auto`).

The required Albeoris.Frameworks source is [vendored](vendor/Albeoris.Frameworks/README.md). No sibling repository or submodule is needed. See [Build and test](docs/developers/building-and-testing.md) for development builds, test projects and optional local inputs.

## Documentation

The [documentation index](docs/README.md) is the starting point for players, mod authors and engine developers.

| Topic | Guides |
| --- | --- |
| Playing | [Controls](docs/players/controls.md), [saves](docs/players/saves.md), [magic rules and Smart Attack](docs/players/magic-system.md), [Debug Room](docs/players/debug-room.md). |
| Mods | [Textures](docs/modding/texture-mods.md), [file replacements](docs/modding/file-replacements.md), [native field mods](docs/modding/field-mods.md). |
| Engine development | [Architecture](docs/developers/architecture.md), [UI](docs/developers/ui-framework.md), [rendering](docs/developers/rendering.md). |
| Tools and automation | [Command line](docs/reference/command-line.md), [environment variables](docs/reference/environment-variables.md), [control API](docs/reference/control-api.md), [automatic exploration](docs/players/automatic-exploration.md). |
| Other resource formats | [Experimental Remastered provider](docs/developers/remastered-resources.md); bounded opening-route support and remaining limitations. |

## Automated releases

Successful pushes to `main` update the `nightly` prerelease. Tags matching `v*` publish versioned releases. GitHub Actions uses the .NET 10 SDK supplied by its Windows runner.

The source is MIT licensed. See [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY.md).

Live scenes use GPU rendering by default. The `--software-renderer` option selects CPU rendering explicitly for controlled debugging; headless previews keep their software renderer. See [rendering and shader development](src/Memoria.FF8.Rendering/README.md#live-gpu-rendering-and-software-diagnostics).
