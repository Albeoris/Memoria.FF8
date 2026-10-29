# Cross-platform desktop migration

[Documentation](../README.md) / [Build and test](building-and-testing.md)

Windows DX11, Vulkan and DX12 share portable services, presentation, launcher and audio. Linux and macOS (Intel and Apple Silicon packages) package the same Native game host with Vulkan. See [graphics hosts](graphics-backends.md) for selection and isolated payloads. A successful cross-publish is not evidence of desktop, audio-device or gameplay acceptance on that OS.

## Build surfaces

Use PowerShell 7 and the SDK selected by `global.json`:

```powershell
./Build.ps1 -Scope Portable -Test
./Build.ps1 -Scope Launcher -RuntimeIdentifier linux-x64
./Build.ps1 -Scope Launcher -RuntimeIdentifier osx-x64
./Build.ps1 -RuntimeIdentifier win-x64
./Build.ps1 -RuntimeIdentifier linux-x64
./Build.ps1 -RuntimeIdentifier osx-x64
./Build.ps1 -RuntimeIdentifier osx-arm64
```

`Portable` compiles `Memoria.FF8.Portable.slnx` against plain `net10.0`, including portable tool and test roots. Its test selection excludes unrelated battle effects. `Launcher` produces a launcher payload under `artifacts/launcher/<RID>`. `Distribution` compiles the complete Native solution graph and packages the launcher, bootstrap, applicable engines and common x64 music worker under `artifacts/package/<RID>`. Windows retains its released layout and immutable updater gate. The [build guide](building-and-testing.md) gives full Native graph commands.

The workflows use Windows x64, Ubuntu 24.04 x64 and native macOS 26 runners for Intel and Apple Silicon. PR validation has read-only permissions. The release matrix creates all four complete artifacts, validates their extracted inventories/native closure and runs selected portable tests. GPU, audio and gameplay acceptance still require the actual target.

## Project coverage

Run `./tools/Get-PlatformInventory.ps1` for a project-level JSON inventory, or add `-Check` to fail when a new project has no coverage assignment. All retained vendored projects remain part of the portable dependency closure. Both product and vendored builds treat CA1416 as an error.

| Projects | Current build/validation surface |
| --- | --- |
| Platform, Runtime, Resources, Assets, Mods, Ui, Events, Rendering, Media, Diagnostics | Portable solution; focused tests on the affected contracts |
| Launcher | Avalonia `net10.0`; separate self-contained win-x64/linux-x64/osx-x64/osx-arm64 publishes |
| Audio and Music | Shared managed parser, sequencer, synthesizer and PCM mixer; native SDL3 device adapter; worker matching the selected architecture |
| TitleScene | Shared Native GPU renderer under `net10.0`; WindowsDX remains a separate build family |
| TitleScene.Preview | Portable `--crest` output and Native Vulkan animated GPU preview |
| Localization, Distribution.Check, Updater.Compatibility | Portable build; per-RID archive/native checks; immutable Windows updater fixture |
| BattleEffects tool | Portable model/coordination build; owned-child supervision explicitly requires Windows Job Objects |
| Portable tests | Included as compile roots; execution selection is explicit in `Build.ps1` |
| App and App.Tests | Shared Native host/integration graph; isolated Windows DX11/Vulkan/DX12 payloads |
| Acceptance | External original-game tooling with explicit local inputs |
| BattleEffects.Tests | Requires an authorized affected-effect selection; never included in aggregate portable execution |

## Architecture decisions

`HostPlatform` describes the operating system, independently of game edition/store identity. `ApplicationPaths` resolves configuration, player saves, cache, logs and staging locations without filesystem operations in constructors. Windows retains its released configuration and player-save locations. Linux uses absolute XDG overrides with standard user-directory fallbacks. macOS uses user Library storage outside the application bundle. Explicit settings and fixture paths continue to take precedence. Existing saves do not change format.

Native dependency descriptors resolve package-relative paths and validate native PE/ELF/Mach-O x64 images before use. A managed AnyCPU PE is not classified as an x86 native library. Universal Mach-O validation requires a contained, actual x86_64 slice. Steam's authenticated SDK image is Windows-only, annotated and selected behind an OS guard. Only Windows distributions include `Data/Steam`; Linux and macOS use local achievements and require no Steam SDK redistributable. This optional integration does not block offline gameplay.

The launcher uses Avalonia 11.3.9 with the fixed Tmds.DBus.Protocol 0.21.3 dependency. Game text and vector/image transport use SkiaSharp/HarfBuzz 3.119.4, without starting a second desktop UI lifetime. Font selection, shaping and rasterization share the same measurement path. SDL3 owns game file pickers and audio devices; Native MonoGame's SDL2 window must never be passed as an SDL3 window. Backend-specific native handles stay in adapters.

The shared music worker stays at `Data/audio-host/Memoria.FF8.Music.exe` on Windows, with a genuine x64 executable and the same command protocol. No compatibility placeholder is used. Original game music, sounds, movies, logs, captures and fixture catalogs are never bundled.

Text run ordering uses the retained RichTextKit UAX #9 resolver; HarfBuzz shapes explicitly segmented direction/script runs. Font inventory is prepared separately from the update/render loop. The [shared audio contract](audio.md) explains the original-format coverage and the deliberate common-bank/Freeverb synthesis choices. Those choices preserve one backend on all hosts rather than relying on platform GM collections or the Windows WavesReverb component.

## M0 and M5 feasibility findings

MonoGame's [3.8.5 native backend announcement](https://monogame.net/blog/2026-07-15-3.8.5-release-2026/) identifies separate Native framework and Vulkan/DX12 runtime families. Version 3.8.5.1 NuGet payloads contain Windows and Linux x64 runtimes and Intel and arm64 slices in the macOS universal runtime. A bounded Windows Vulkan probe created and presented five clear frames successfully. This establishes device/window initialization only; it does not establish game rendering.

The inspected `MonoGame.Runtime.Mac.Vulkan` 3.8.5.1 Intel slice declares macOS 26.0 in `LC_BUILD_VERSION`. An older baseline requires a separately verified runtime build or compatible upstream package. The Intel CI runner builds and statically validates the complete bundle, including executable modes, native imports and deployment versions. An older runner OS cannot establish macOS 26 runtime acceptance. Cocoa lifecycle, GPU/audio behavior and Metal portability require target acceptance.

The pinned `dotnet-mgfxc` 3.8.5.1 accepts `Vulkan` and `DirectX_12` profiles. Shared shader sources now retain shader model 4 for DX11 and select shader model 6 with explicit texture/sampler registers for Vulkan. Generated payloads are isolated and associated with source/profile/compiler hashes. See [shader generation](shaders.md).

Windows graphics hosts live in separate backend directories and select their framework/runtime family before process startup. Keep WindowsDX and Native restore/build outputs separate. The dispatcher supplies the shared package data root for mods, configuration and helpers instead of deriving it from the nested backend executable directory.

## Media tools and external acceptance

FFmpeg and FFprobe are optional separately installed tools. Windows uses the existing checksummed BtbN LGPL ZIP setup. Linux setup selects the checksummed BtbN linux64 LGPL archive and preserves executable modes; that distribution requires glibc 2.28 and kernel 4.18 or newer. FFmpeg does not publish macOS binaries itself. The macOS launcher first offers the LGPL variant of [dfloresc/ffmpeg-macos-builds v9.0.1-1](https://github.com/dfloresc/ffmpeg-macos-builds/releases/tag/v9.0.1-1) for the launcher's architecture (`arm64` or `x86_64`, macOS 12 or newer), pinned by SHA-256. That release is built in CI from checksum-pinned upstream sources without GPL or nonfree components and publishes the corresponding sources. Its tools load shared libraries through `@executable_path/../lib`, so setup installs `bin/ffmpeg`, `bin/ffprobe`, `lib/` (with the library version links), `LICENSES/` and `BUILDINFO.txt` together in the media-tools directory and skips the headers; on macOS, tool lookup falls back to `bin/<tool>` when no top-level tool exists. Setup records the entries it installed and never replaces a `bin/` or `lib/` it did not create. The installed tools must pass the usual LGPL validation and list the encoders and muxers playback uses. If the download or validation fails, the launcher offers to build FFmpeg 9.0.2 from the official source release instead: it verifies the pinned SHA-256, builds natively for the launcher's architecture with LGPL-only, system-library configuration (Xcode Command Line Tools required), and keeps the license notices and build log beside the tools. The build keeps FFmpeg's default decoders, demuxers, parsers and filters, but enables only the encoders (`rawvideo`, `pcm_s16le`) and muxers (`rawvideo`, `s16le`, `ivf`) that playback writes, without capture devices or network protocols; setup checks that the built `ffmpeg` lists them. After 60 seconds without new headers or data, setup reports that the transfer may be stalled and keeps waiting. Cancel setup or close the launcher to cancel and wait for cleanup. A matching native LGPL pair can also be installed manually. Unix executables have no `.exe` suffix. No installation executes a Windows game or requires Wine.

Selection validates an explicitly configured directory first, then a valid managed installation, then a paired installation in a local absolute PATH directory. Discovery and executable validation run asynchronously; no PATH or system installation is modified. Both tools must have the current native x64 ABI and report their build configuration. GPL and nonfree configurations are rejected. An invalid managed pair does not hide a valid PATH pair; automatic repair writes only to the managed destination.

Portable CI, package ABI checks and software-rendered output are separate from real-target acceptance. Linux GPU/window/fullscreen, controllers, audio synchronization and gameplay stability require native Linux hardware. Intel macOS startup, audio and graphics require an Intel Mac; Apple Silicon startup, audio and graphics require an Apple Silicon Mac. WSL2 testing is a development convenience and cannot close those gates.

## Distribution layouts and prerequisites

Windows preserves Memoria.Launcher.exe, Data/Memoria.FF8.exe and the real x64 worker at Data/audio-host/Memoria.FF8.Music.exe. Data/Game/dx11, vulkan and dx12 have separate runtime families and matching shaders. Linux uses corresponding extensionless executables and only Data/Game/vulkan. Extract Memoria-FF8-linux-x64.tar.gz with a mode-preserving archive tool and start ./Memoria.Launcher. A separately installed .NET runtime is unnecessary. Ubuntu 24.04 x64 is the CI baseline. A Vulkan-capable driver, desktop session, font/fontconfig, X11/Wayland dependencies and working audio are required; [Linux testing](linux-testing.md) gives explicit dependency setup and diagnostics.

macOS requires macOS 26.0 and a Metal-capable Intel or Apple Silicon Mac compatible with MoltenVK. Extract `Memoria-FF8-osx-x64.tar.gz` on Intel or `Memoria-FF8-osx-arm64.tar.gz` on Apple Silicon and move Memoria.FF8.app to a writable location. Its executable root is Contents/MacOS, with bootstrap/game/audio under Data; Contents/Info.plist declares the deployment baseline. Both architectures use the same bundle layout with matching native executables and support updates from their matching release archive. Builds include official [MoltenVK 1.4.2](https://github.com/KhronosGroup/MoltenVK/releases/tag/v1.4.2), verified against a pinned SHA-256, and its license. The app selects that bundled library explicitly; no Vulkan SDK or Homebrew installation is required. Artifacts are unsigned and not notarized; Gatekeeper approval remains a user action. Signing credentials must stay outside the repository and PR jobs.

Descriptors record RID, layout, entry points, complete SHA-256 inventory and Unix modes. Windows retains schema 1 for released-updater compatibility; Unix uses schema 2. The music worker and every offered graphics host are mandatory inventory entries. Validation rejects links, traversal, duplicate/colliding entries, excessive expansion and foreign native architecture. macOS static validation checks bundled Mach-O imports and LC_RPATH, rejects developer-machine paths, verifies the Vulkan/Metal provider and compares native deployment versions with Info.plist.

Mac release stamping updates both Apple bundle version keys to the release's first three numeric components. The complete tag, including an optional fourth revision, remains in `MemoriaReleaseVersion` and the distribution manifest. Local/nightly bundle versions use `0.0.0` with their explicit channel label. Reusing a tested nightly for a release changes only the distribution descriptor and Info.plist, updates that file's inventory hash/size, and verifies every other payload hash and mode. All bundle metadata mutations must finish before any future signing/notarization stage.

Configuration, saves, caches, logs and dependency setup belong in per-user locations outside read-only installations and application bundles. Select legal installed game files through the launcher. Offline play uses installed payloads; network availability affects update discovery/download. Keep saves and custom mods outside disposable build directories.

On macOS, user mod packages and `mods.ini` belong in `~/Library/Application Support/Memoria/FF8/Mods`, outside the application bundle. The launcher and direct game starts share this default; an explicit `--mods-dir` selects another directory. Windows and Linux continue to use the package’s adjacent `Mods` directory. Keep the macOS `.app` immutable so whole-bundle updates can verify and replace it safely.
