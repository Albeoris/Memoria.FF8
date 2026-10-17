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

Memoria FF8 is an experimental, standalone Windows engine for FINAL FANTASY VIII. It reads resources from your own installed copy; game data, movies, music and saved games are not included. The supported playable edition is **English Steam 2013 on Windows x64**. Gameplay, battle behavior and rendering remain under development. English Remastered also supports the verified opening route from New Game into the classroom; further gameplay remains experimental and unimplemented contracts stop with an explicit error.

Watch the [gameplay video](https://youtu.be/QnqDSBLkN58).

## Play

Download the Windows archive from [Releases](https://github.com/Albeoris/Memoria.FF8/releases), extract it, and open `Memoria.Launcher.exe`. Keep the adjacent `Data/` directory intact. The launcher discovers installations, offers manual folder selection, sets up FFmpeg with your consent and remembers your last successful selection.

Follow [Installation and first launch](docs/players/getting-started.md) for requirements and troubleshooting. The release includes the required .NET runtimes.

## Build

From a source checkout, use Windows, PowerShell 7 and the .NET 10 SDK:

```powershell
./Build.ps1 -Test
```

The result is `artifacts/Memoria-FF8-win-x64.zip`, with this layout:

```text
Memoria.Launcher.exe
Data/
Mods/
  Tonberry/
  Demaster/
  Junction/
```

The three mod directories are empty in the release archive and ready for user-installed mods.

The required Albeoris.Frameworks source is [vendored](vendor/Albeoris.Frameworks/README.md). No sibling repository or submodule is needed. See [Build and test](docs/developers/building-and-testing.md) for development builds, test projects and optional local inputs.

## Documentation

The [documentation index](docs/README.md) is the starting point for players, mod authors and engine developers.

| Topic | Guides |
| --- | --- |
| Playing | [Controls](docs/players/controls.md), [saves](docs/players/saves.md), [Debug Room](docs/players/debug-room.md). |
| Mods | [Textures](docs/modding/texture-mods.md), [file replacements](docs/modding/file-replacements.md). |
| Engine development | [Architecture](docs/developers/architecture.md), [UI](docs/developers/ui-framework.md), [rendering](docs/developers/rendering.md). |
| Tools and automation | [Command line](docs/reference/command-line.md), [environment variables](docs/reference/environment-variables.md), [control API](docs/reference/control-api.md), [automatic exploration](docs/players/automatic-exploration.md). |
| Other resource formats | [Experimental Remastered provider](docs/developers/remastered-resources.md); bounded opening-route support and remaining limitations. |

## Automated releases

Successful pushes to `main` update the `nightly` prerelease. Tags matching `v*` publish versioned releases. GitHub Actions uses the .NET 10 SDK supplied by its Windows runner.

The source is MIT licensed. See [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY.md).

Live scenes use GPU rendering by default. The `--software-renderer` option selects CPU rendering explicitly for controlled debugging; headless previews keep their software renderer. See [rendering and shader development](src/Memoria.FF8.Rendering/README.md#live-gpu-rendering-and-software-diagnostics).
