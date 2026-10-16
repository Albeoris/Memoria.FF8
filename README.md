# Memoria FF8

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

Open `Memoria.FF8.slnx` for development. Individual test executables support the xUnit command line through `dotnet run --project tests/<project>`. Tests that use original resources require `MEMORIA_FF8_GAME_DIR`; saved-state fixtures, where needed, are supplied separately through `MEMORIA_FF8_TEST_FIXTURES`. External acceptance checks require a recording workspace through `MEMORIA_FF8_RECOMP_WORKSPACE` and the `recomp` tool (or its executable path in `RECOMP`).

The required Albeoris.Frameworks source closure is vendored, with its upstream revision and retained changes documented in [vendor/Albeoris.Frameworks/README.md](vendor/Albeoris.Frameworks/README.md). Building needs no sibling repository or submodule. Package versions are centralized in `Directory.Packages.props`. Ordinary builds use the checked-in compiled title shader; its project README describes shader regeneration.

## Automated builds

Pushes to `main` build, test and replace the prerelease `nightly` download. Tags matching `v*` publish a versioned release. GitHub Actions uses the .NET 10 SDK already installed on its Windows runner.

The source is MIT licensed; see [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY.md).
