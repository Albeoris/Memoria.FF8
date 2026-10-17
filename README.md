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

Download the Windows archive from this repository's Releases, extract it, and open `Memoria.Launcher.exe`. Keep the adjacent `Data` directory intact. The launcher finds installed copies, lets you choose a game folder, installs required FFmpeg tools on request and starts the engine. Only the English Steam 2013 edition is currently playable; discovery of another edition does not establish support. Windows 10 or later on x64 hardware is required; the bundled x86 audio process handles original DirectMusic playback. A separate .NET installation is unnecessary for the published application.

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

The required Albeoris.Frameworks source closure is vendored, with its upstream revision and retained changes documented in [vendor/Albeoris.Frameworks/README.md](vendor/Albeoris.Frameworks/README.md). Building needs no sibling repository or submodule. Package versions are centralized in `Directory.Packages.props`. Ordinary builds use the checked-in compiled title shader; its project README describes shader regeneration.

## Automated builds

Pushes to `main` build, test and replace the prerelease `nightly` download. Tags matching `v*` publish a versioned release. GitHub Actions uses the .NET 10 SDK already installed on its Windows runner.

The source is MIT licensed; see [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY.md).
