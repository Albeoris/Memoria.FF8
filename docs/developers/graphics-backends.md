# Windows graphics hosts

The Windows distribution builds Direct3D11 and Vulkan from the same App, TitleScene and gameplay sources. Backend differences are limited to MonoGame package selection, shader resources and platform adapters. There is one implementation of battle effects, geometry submission, audio and gameplay.

`Data/Memoria.FF8.exe` selects a host before starting it. Separate payloads under `Data/Game/dx11/` and `Data/Game/vulkan/` keep incompatible MonoGame assemblies and native libraries apart. Both hosts use the shared `Data/audio-host/`, `Data/Steam/` and bundled mods. The dispatcher forwards paths, arguments and output, waits for the actual game's exit and owns its child-process lifetime.

Choose Auto, Direct3D11 or Vulkan in the launcher or Config → Graphics. An in-game change requires restart. The command-line `--graphics-backend` override takes precedence over `Graphics.Backend` in the existing UI settings file, then Auto. Direct3D12 is recognized in the settings contract but unavailable until its own milestone. Unix launchers expose Auto/Vulkan, while complete Unix game hosts remain later milestones.

```powershell
./Build.ps1
./artifacts/package/Data/Memoria.FF8.exe --graphics-backend Vulkan --game-dir $env:MEMORIA_FF8_GAME_DIR
./artifacts/package/Data/Memoria.FF8.exe --graphics-backend Direct3D11 --game-dir $env:MEMORIA_FF8_GAME_DIR
```

Windows Auto tries Direct3D11 first, then Vulkan only after a recorded graphics initialization failure or unavailable host. Explicit selection never falls back. Invalid settings, missing game resources, unknown startup failures and initialization timeouts do not authorize fallback. Once a host presents its first frame, its exit or crash cannot cause a restart on another backend. The launcher confirms startup only after the matching host's readiness receipt.

Diagnostics record requested/actual backend, each initialization attempt, native device identity, HiDef/target formats and sample counts, stdout/stderr and exit status. They default to the user's application logs directory; `MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY` selects a separate diagnostic destination. The native window adapter resolves the Windows window owned by the game thread; it never treats MonoGame Native's SDL2 window pointer as an HWND or passes it to SDL3.

For separate development builds use different intermediate and output trees:

```powershell
dotnet build src/Memoria.FF8.App -p:MemoriaGraphicsBackend=Direct3D11 --artifacts-path artifacts/build/dx11
dotnet build src/Memoria.FF8.App -p:MemoriaGraphicsBackend=Vulkan --artifacts-path artifacts/build/vulkan
```

The packaged dispatcher supplies the shared data directory. For direct nested-host debugging set `MEMORIA_FF8_APPLICATION_DIRECTORY` to the absolute package `Data` directory. Do not merge host outputs. See [shader generation and validation](shaders.md) and [build checks](building-and-testing.md). A successful capability probe establishes startup support, not complete scene or gameplay acceptance.
