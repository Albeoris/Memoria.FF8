# Cross-platform desktop migration

[Documentation](../README.md) / [Build and test](building-and-testing.md)

The migration keeps the current Windows DX11 game host usable while portable services, presentation, launcher and audio are introduced. The Linux and Intel macOS game hosts start in a later milestone. A successful cross-publish is not evidence of desktop, audio-device or gameplay acceptance on that OS.

## Build surfaces

Use PowerShell 7 and the SDK selected by `global.json`:

```powershell
./Build.ps1 -Scope Portable -Test
./Build.ps1 -Scope Launcher -RuntimeIdentifier linux-x64
./Build.ps1 -Scope Launcher -RuntimeIdentifier osx-x64
./Build.ps1 -RuntimeIdentifier win-x64
```

`Portable` compiles `Memoria.FF8.Portable.slnx` against plain `net10.0`, including portable tool and test roots. Its test selection covers the migrated contracts and excludes unrelated battle effects. `Launcher` produces a self-contained x64 launcher payload under `artifacts/launcher/<RID>`; it is not a complete playable distribution. The default `Distribution` scope retains the released Windows package layout and frozen updater gate. Unsupported complete Linux/macOS distributions fail with an explicit diagnostic.

The `portable.yml` workflow builds/tests on Windows, Ubuntu 24.04 and an explicitly Intel macOS runner. These jobs have read-only permissions and publish development launcher payloads. Complete releases remain Windows-only until the Native game hosts, target acceptance and portable update transactions are available.

## Project coverage

Run `./tools/Get-PlatformInventory.ps1` for a project-level JSON inventory, or add `-Check` to fail when a new project has no coverage assignment. All retained vendored projects remain part of the portable dependency closure. Both product and vendored builds treat CA1416 as an error.

| Projects | Current build/validation surface |
| --- | --- |
| Platform, Runtime, Resources, Assets, Mods, Ui, Events, Rendering, Media, Diagnostics | Portable solution; focused tests on the affected contracts |
| Launcher | Avalonia `net10.0`; separate self-contained win-x64/linux-x64/osx-x64 publishes |
| Audio and Music | Shared managed parser, sequencer, synthesizer and PCM mixer; native SDL3 device adapter; x64 worker |
| TitleScene | Portable vector/raster helpers under `net10.0`; current WindowsDX GPU renderer under `net10.0-windows` |
| TitleScene.Preview | Portable `--crest` output; Windows target retains animated GPU preview |
| Localization, Distribution.Check, Updater.Compatibility | Portable build; Windows distribution/update execution remains the current package contract |
| BattleEffects tool | Portable model/coordination build; owned-child supervision explicitly requires Windows Job Objects |
| Portable tests | Included as compile roots; execution selection is explicit in `Build.ps1` |
| App and App.Tests | Current Windows DX11 host and its integration tests |
| Acceptance | External original-game tooling with explicit local inputs |
| BattleEffects.Tests | Requires an authorized affected-effect selection; never included in aggregate portable execution |

## Architecture decisions

`HostPlatform` describes the operating system, independently of game edition/store identity. `ApplicationPaths` resolves configuration, player saves, cache, logs and staging locations without filesystem operations in constructors. Windows retains its released configuration and player-save locations. Linux uses absolute XDG overrides with standard user-directory fallbacks. macOS uses user Library storage outside the application bundle. Explicit settings and fixture paths continue to take precedence. Existing saves do not change format.

Native dependency descriptors resolve package-relative paths and validate native PE/ELF/Mach-O x64 images before use. A managed AnyCPU PE is not classified as an x86 native library. Universal Mach-O validation requires a contained, actual x86_64 slice. Steam's currently authenticated SDK image is Windows-only, annotated and selected behind an OS guard; other hosts retain local achievements.

The launcher uses Avalonia 11.3.9 with the fixed Tmds.DBus.Protocol 0.21.3 dependency. Game text and vector/image transport use SkiaSharp/HarfBuzz 3.119.4, without starting a second desktop UI lifetime. Font selection, shaping and rasterization share the same measurement path. SDL3 owns game file pickers and audio devices; Native MonoGame's SDL2 window must never be passed as an SDL3 window. Backend-specific native handles stay in adapters.

The shared music worker stays at `Data/audio-host/Memoria.FF8.Music.exe` on Windows, with a genuine x64 executable and the same command protocol. No compatibility placeholder is used. Original game music, sounds, movies, logs, captures and fixture catalogs are never bundled.

Text run ordering uses the retained RichTextKit UAX #9 resolver; HarfBuzz shapes explicitly segmented direction/script runs. Font inventory is prepared separately from the update/render loop. The [shared audio contract](audio.md) explains the original-format coverage and the deliberate common-bank/Freeverb synthesis choices. Those choices preserve one backend on all hosts rather than relying on platform GM collections or the Windows WavesReverb component.

## M0 and M5 feasibility findings

MonoGame's [3.8.5 native backend announcement](https://monogame.net/blog/2026-07-15-3.8.5-release-2026/) identifies separate Native framework and Vulkan/DX12 runtime families. Version 3.8.5.1 NuGet payloads contain Windows and Linux x64 runtimes and an Intel slice in the macOS universal runtime. A bounded Windows Vulkan probe created and presented five clear frames successfully. This establishes device/window initialization only; it does not establish game rendering.

The inspected `MonoGame.Runtime.Mac.Vulkan` 3.8.5.1 Intel slice declares macOS 26.0 in `LC_BUILD_VERSION`. An older Intel macOS baseline therefore requires a separately verified runtime build or compatible upstream package; do not infer compatibility from the package RID. The current Intel macOS CI runner validates the portable foundation and launcher, not this game runtime. Actual Cocoa lifecycle, relocation and Metal portability checks remain target gates.

The pinned `dotnet-mgfxc` 3.8.5.1 accepts `Vulkan` and `DirectX_12` profiles. Existing game shaders use shader model 4 and legacy `tex2D` sampling. Vulkan compilation rejects shader model 4; changing only the profile to shader model 6 then rejects legacy sampling. M5 needs a deliberate shared shader-source adaptation and isolated compiled payloads, rather than reusing WindowsDX shader binaries. This work is not part of the current host implementation.

Final graphics hosts will live in separate backend directories and select their framework/runtime family before process startup. Keep WindowsDX and Native restore/build outputs separate. Preserve the package root for data, mods, configuration and helpers instead of deriving it from the nested backend executable directory.

## Media tools and external acceptance

FFmpeg and FFprobe are optional separately installed tools. Windows uses the existing checksummed BtbN LGPL ZIP setup. Linux setup selects the checksummed BtbN linux64 LGPL archive and preserves executable modes; that distribution requires glibc 2.28 and kernel 4.18 or newer. macOS currently provides an actionable manual installation route for matching Intel x64 FFmpeg/FFprobe, with the selected distribution's license and corresponding-source notices. Unix executables have no `.exe` suffix. No installation executes a Windows game or requires Wine.

Portable CI, package ABI checks and software-rendered output are separate from real-target acceptance. Linux GPU/window/fullscreen, controllers, audio synchronization and gameplay stability require native Linux hardware. Intel macOS startup, audio and graphics require an Intel Mac. WSL2 testing is a development convenience and cannot close those gates.
