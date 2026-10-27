# Test Linux from Windows

From the public product directory, after the one-time setup:

```powershell
pwsh ./tools/Test-Linux.ps1
```

The default detects an installed WSL2 distro, preferring Ubuntu 24.04, and uses WSLg. It probes the x64 host, builds or verifies a matching cached Linux payload, transfers changed files, runs portable tests and opens the application for interaction. Close the application to collect its exit status and diagnostics; Ctrl+C cancels. The default guard timeout is one hour. Use `-Phase Test` for a quick 15-second smoke instead. This currently validates the launcher, shared presentation services and audio implementation. The Linux game host is not implemented; no launcher, Vulkan cube or audio result is recorded as successful gameplay.

WSLg uses a virtualized graphics path. Native Linux/Steam Deck graphics acceptance remains a separate hardware gate.

## One-time WSL setup

Use Windows 11 or a Windows version supporting WSLg. Follow Microsoft's [WSL installation](https://learn.microsoft.com/en-us/windows/wsl/install) and [Linux GUI application setup](https://learn.microsoft.com/en-us/windows/wsl/tutorials/gui-apps). From an administrator PowerShell, **you** may run:

```powershell
wsl --install -d Ubuntu-24.04
```

Restart if Windows requests it, launch Ubuntu once, and create its Linux user. Confirm `wsl --list --verbose` reports version 2. The testing script does not enable Windows features, install a distro, update graphics drivers, restart WSL or change Windows settings.

The Windows checkout needs the repository's .NET SDK, PowerShell 7 and Git. The distro needs a .NET 10 SDK on PATH and ordinary desktop runtime libraries. The SDK runs the staged test modules through `dotnet test`; Python is not required. Install the SDK using the supported instructions for your distribution before running this workflow. To explicitly authorize installing Ubuntu runtime and diagnostic packages from its configured standard repositories:

```powershell
pwsh ./tools/Test-Linux.ps1 -SetupDependencies
```

This asks `sudo apt-get` to install DejaVu/Noto core and CJK fonts, X11/font/audio/GTK libraries, standard desktop-portal services, Vulkan tools, Mesa utilities and the standard `mesa-vulkan-drivers` package containing Lavapipe. It does not install the .NET SDK or add repositories, experimental packages or vendor GPU drivers. Later runs reuse these packages. Without this flag the script performs no package installation.

Create optional local settings, which Git ignores:

```powershell
Copy-Item ./tools/linux-testing/settings.example.json ./tools/linux-testing/local.settings.json
```

Set `GameDirectory` to your existing legal Windows game folder, including a folder on another drive. The script invokes `wslpath` with that complete path as one argument; it does not assume a C: drive or split spaces. It prefills the isolated test launcher's selected folder. It never copies game assets into a build or alters the selected folder. If a mapped or external drive is not available inside WSL, expose/mount that drive yourself and verify it is accessible before retrying. The script does not mount drives automatically.

`-Distro` overrides the configured distro. `-GameDirectory` overrides its configured Windows game folder for a single run. For example, pass a folder selected on your own machine:

```powershell
pwsh ./tools/Test-Linux.ps1 -GameDirectory $env:MEMORIA_FF8_GAME_DIR
```

## Phases and evidence

| Command | Behavior |
| --- | --- |
| `pwsh ./tools/Test-Linux.ps1` | Detect, build/reuse, deploy, portable tests and interactive launch. |
| `pwsh ./tools/Test-Linux.ps1 -Phase Diagnostics` | Build/reuse the small .NET helper and gather available Linux graphics/system diagnostics without building the application. |
| `pwsh ./tools/Test-Linux.ps1 -Phase Build` | Build/verify Linux payload from Windows; no installed WSL/SSH target required. |
| `pwsh ./tools/Test-Linux.ps1 -Phase Deploy` | Deploy a matching previously built payload. |
| `pwsh ./tools/Test-Linux.ps1 -Phase Test` | Verify/deploy a matching cache and run tests plus a 15-second smoke. |
| `pwsh ./tools/Test-Linux.ps1 -Phase Launch` | Verify/deploy and open the application for manual interaction until exit or timeout. |
| `pwsh ./tools/Test-Linux.ps1 -Component Audio` | Play explicit `DlsPath`/`SgtPath` fixtures through the common audio harness. |

Build identity hashes tracked and untracked nonignored working files, deleted files, dependency pins, scripts, capability declarations and the SDK version. Source identity is checked again before sealing the build receipt; edits during publishing cannot produce a falsely verified cache. Reuse additionally verifies every payload file against its SHA-256 manifest. A corrupted cache is retained under a separate diagnostic name. Current launcher builds and test publishes use an isolated reusable output tree. Five Linux test modules are staged: Platform, Audio, Launcher, Presentation and LinuxTesting. The target runs them with `dotnet test --root-directory <module-directory> --test-modules <module-name>.dll --results-directory <results-directory> --no-progress`, using the staged `global.json` to select Microsoft Testing Platform. Successful portable test results are reused on the same target only for the same verified build fingerprint; failures are not cached as successes. Optional original-resource tests retain their explicit fixture requirements and skip when absent. No battle-effect test project is selected.

Evidence is stored under `artifacts/linux-test/runs/<run-id>/`: orchestration/build/transport logs, per-project portable test logs, `result.json`, system information, Vulkan ICD descriptors, full Vulkan surface/device information and presentation-probe results. Process stdout/stderr and nonzero exit status are preserved. The console prints target failures and structured status. Configuration, cache and test state are separate from ordinary launcher/player data. The source tree never contains copied original game assets or credentials.

Smoke success means the requested process survived its interval. A launcher that exits early, even with exit code zero, does not pass the smoke. Manual launch preserves the actual exit code; its timeout returns 124. The .NET helper uses standard `Process` APIs and `Kill(entireProcessTree: true)` on timeout or cancellation, followed by bounded cleanup and evidence retrieval. Remote cancellation writes `stop.request` in the owned run directory; the helper observes it during tests, diagnostics and smoke execution, then writes `result.json`. Cleanup applies to a running process tree; detached descendants whose parent has already exited are outside this contract. There is no custom process-group manager, `/proc` traversal or application P/Invoke.

Screenshots use an already installed `grim`, `spectacle` or `gnome-screenshot`, or an explicitly configured `ScreenshotCommand` argument array with `{output}` for the output filename. If no suitable command exists, the result records screenshots as unavailable. A screenshot still needs human inspection.

## Vulkan diagnostics

```powershell
pwsh ./tools/Test-Linux.ps1 -Vulkan Software
pwsh ./tools/Test-Linux.ps1 -Mode Hardware -Target SteamDeck -Vulkan Hardware
```

`Auto` inventories installed ICD JSON files, runs bounded `vulkaninfo --summary` and full `vulkaninfo`, and attempts a bounded 30-frame `vkcube` presentation probe. `Software` requires exactly one existing Lavapipe descriptor and sets both Vulkan driver-selection environment variables to that exact file. `Hardware` fails if only a CPU/software device is identified. `None` skips Vulkan execution while retaining the ICD inventory. CPU llvmpipe/Lavapipe results and hardware results have distinct classifications. Missing tools/session support is reported, not installed implicitly. These probes establish only Vulkan-tool behavior; Memoria's Vulkan renderer acceptance remains pending its game-host milestone.

## Steam Deck or another native Linux host

The target must already have an x64 Linux graphical session, OpenSSH access, a .NET 10 SDK on PATH and the required runtime libraries. Windows needs OpenSSH `ssh` and `scp` supporting SFTP (`-s`). Configure authentication and host-key trust using normal OpenSSH configuration first. The script uses batch authentication and does not store passwords, alter host keys, unlock SteamOS, install packages or start a streaming server.

Add entries under `Targets` in local settings. `-Target SteamDeck` selects `Targets.SteamDeck`; another name selects that exact entry. Each entry may set `SshHost` (an SSH alias), `TargetDirectory`, `HardwareGameDirectory`, `HardwareDlsPath`, `HardwareSgtPath`, `GraphicalEnvironment`, `StreamCommand` and `ScreenshotCommand`. The sample has no machine-specific account or path.

```powershell
pwsh ./tools/Test-Linux.ps1 -Mode Hardware -Target SteamDeck
```

The configured staging directory must be dedicated to this tool. An existing nonempty directory without the tool's ownership marker is rejected. Payload manifests reject traversal and symlink paths before changing files. Unknown or edited staging files are preserved and cause a diagnostic requiring a fresh staging directory. Incremental deployment sends only changed binary/content files in a binary-only transfer archive. Changed files are hash-verified into a separate pending tree; the active payload and receipt are promoted together through a recoverable journal. Interrupted transfers can retry the same build, reusing already verified files. Interrupted promotion completes or restores the previous verified payload before another deployment. Edited or unknown files are preserved and rejected. Original game data is excluded from transfer archives. Existing legal game data is referenced through `HardwareGameDirectory` and is never included in payload synchronization.

For an explicit one-time copy of the configured Windows `GameDirectory` to a new target asset directory:

```powershell
pwsh ./tools/Test-Linux.ps1 -Mode Hardware -Target SteamDeck -SyncGameAssets
```

An existing target `assets/game` directory is preserved and rejected for this one-time operation. Afterwards set `HardwareGameDirectory` to the printed target path and omit `-SyncGameAssets`. Subsequent binary updates do not transfer assets again.

The runner attaches to an existing session using `DISPLAY`/`WAYLAND_DISPLAY` and `XDG_RUNTIME_DIR`; it can discover an existing local Wayland socket or X11 display 0. Supply `GraphicalEnvironment` when your session differs. `StreamCommand` is an optional Windows executable-and-arguments array that starts an already configured streaming client, such as a client for an existing Sunshine/Moonlight installation. The script installs/configures neither client nor server and stops only the client it started. Configure target session and streaming once, then use the same command for subsequent builds.

## Current boundary and workflow tests

The committed [build capability profile](../../tools/linux-testing/build-capabilities.json) declares `gameHost: false`. `-Component Game` fails explicitly. When a real supported Linux distribution recipe exists, update this profile to its actual `Distribution` output and game executable; the same workflow can then select that artifact automatically. Filename guesses or a running launcher cannot enable gameplay acceptance.

Run orchestration tests without an installed distro or SSH target:

```powershell
pwsh ./tools/linux-testing/tests/Orchestration.Tests.ps1
dotnet test --project ./tests/Memoria.FF8.LinuxTesting.Tests -c Release --no-progress
```

These validate quoting, Windows drive conversion, named-target selection, dirty-source/dependency/script cache invalidation, tamper detection, incremental transfer plans, data preservation, process timeout/cancellation and Vulkan classification. They do not establish an actual WSLg, Steam Deck, Intel Mac or native Linux runtime pass.

CI runs the PowerShell orchestration checks and .NET tests on Windows, Linux and macOS. Tests requiring native Linux fixtures carry `Trait("Platform", "Linux")` and explicitly skip on other hosts. Portable file operations and mocked Linux command contracts run on every host. The Linux helper is built from [Memoria.FF8.LinuxTesting](../../tools/Memoria.FF8.LinuxTesting/Memoria.FF8.LinuxTesting.csproj); there is no Python runner. Existing recognized deployment ownership markers and pending deployment journals remain compatible.
