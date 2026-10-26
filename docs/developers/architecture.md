# Engine architecture

Memoria runs its own game session, event interpreter, simulation and presentation using resources from a user-selected installation. The launcher selects the installation and starts the application. It does not host the simulation. See [getting started](../players/getting-started.md) and the [command-line reference](../reference/command-line.md) for entry points.

## Projects and ownership

| Project | Responsibility |
| --- | --- |
| `Memoria.Launcher` | Avalonia desktop launcher, installation selection and application startup. |
| `Memoria.FF8.App` | MonoGame host, input, scene coordination, menus, resource lifetime, control queue and diagnostics. `RecreateGame` owns the window; `GameWorld` connects the engine components. |
| `Memoria.FF8.Runtime` | Renderer-independent session/state contracts, save validation, world state, battles, cards and shared game rules. `GameSession` owns persistent `GameState` and implements `IEventServices`. |
| `Memoria.FF8.Events` | Field event execution, actors, script scheduling, movement, dialogue and ordinary `PlayerAction` dispatch. `EventEngine` captures and restores its current VM state. |
| `Memoria.FF8.Resources` | Shared resource lookup, providers and resource image support. |
| `Memoria.FF8.Assets` | Installed archive/resource decoding and conversion into typed engine data. |
| `Memoria.FF8.Rendering` | CPU scene rasterization and field/world/battle presentation support. It consumes decoded resources and current state. |
| `Memoria.FF8.Ui` | Renderer-independent retained UI elements, layout, focus and input. |
| `Memoria.FF8.Media` | Movie/media tool integration. |
| `Memoria.FF8.Platform` | Host OS, user paths, asynchronous desktop contracts and native dependency validation. |
| `Memoria.FF8.Audio` | Shared SGT/DLS parsing, sequencing, synthesis, PCM decoding/mixing and SDL3 device output. |
| `Memoria.FF8.Music` | Portable x64 music worker. The app builds and distributes the helper separately. |
| `Memoria.FF8.TitleScene` | Procedural title scene and its presentation resources. |
| `Memoria.FF8.Mods` | Mod discovery, manifests, selection and resource overlays. |

Engine libraries, launcher and music helper target plain .NET 10. The current DX11 application and its GPU title renderer retain Windows targets; the title vector/raster helpers also have a portable target. The vendored `Albeoris.Frameworks` projects supply format readers and related dependencies; project references resolve inside the product tree. They do not require another checkout. See [Cross-platform desktop migration](cross-platform.md) for complete project coverage and remaining host boundaries.

## Session and frame flow

1. Startup parses options, loads a standalone state when requested and opens the selected installation through `GameResourceHost`.
2. `RecreateGame` creates a `GameSession` and `GameWorld`. `GameWorld.EnsureField` prepares the appropriate field/world allocation and restores the saved VM where applicable.
3. Keyboard, controller and local control requests become typed inputs. Ordinary field actions pass through `GameWorld.Apply` and `EventEngine.Apply`; world, tutorial, card and battle owners handle their active input contracts.
4. Simulation advances state and returns a `PlayerActionResult` with the next observable trigger. Scene transitions preserve departing state until the destination allocation is ready. Presentation must not assume the requested destination has already been drawn.
5. Drawing reads the current state, rasterizes the supported scene and presents it through MonoGame. Menu layout and drawing use the UI adapter. Audio and decoded movie playback have their own host lifetimes.
6. Saving captures current engine owners into a standalone `GameState` and publishes JSON atomically. Loading validates the state and restores those owners; it does not reconstruct previous gameplay.

Ordinary event simulation uses a 30 Hz clock. Window updates can catch up multiple simulation steps before one draw. UI overlays suspend ordinary simulation, and movie pause/resume keeps its clock and output together. Control-step mode suppresses automatic field/world simulation while leaving the host active; movies retain their playback-driven behavior. See the [control API](../reference/control-api.md) for these distinctions.

## State boundaries

`GameState` holds persistent session state and serialized event state. Current process owners, scene owners and saved values have explicit contracts; they are not interchangeable copies of a single global dictionary. `GameSession.Load` validates consistency between root state and the embedded VM. `GameWorld` restores resource-dependent owners and checks that continuation is supported.

Keep simulation mutations in their owning transaction. A menu projects live state into `MenuData` and submits a supported action; drawing does not edit inventory, party state or event variables. Debug presentation overrides change the displayed scene without changing actor simulation or story state. Host preferences and control bindings are separate from game saves.

The current persistence model is described in [saves and snapshots](../players/saves.md). An older save that lacks required state needs an explicit, separately recorded migration. Do not invent defaults or replay earlier travel during load.

Field model-part snapshots retain the registered model ID and sparse logical translation progress. Static part tables, model hashes, file offsets and initialized skin flags come from the selected resource. Executed draw samples retain their logical callback input; rendering derives current skin bindings without advancing live counters. Active translations still require their referenced part to exist in the selected model.

When selected model resources change, inactive native presentation caches are reconstructed through the resource registration contract while retaining actor presentation, sampled animation phase, VM state and random state. Active eye commands, known texture-copy progress and pending VRAM work currently require a separate semantic transfer contract and fail explicitly. Unchanged resource selections retain their existing process state. Historical snapshots containing copied part tables require an explicit migration.

## Extending the engine

- Add decoded resource contracts to the format/resource layer and pass typed data to the owning subsystem. Keep resource selection configurable through the installed-resource host and [mod providers](../modding/file-replacements.md).
- Implement event behavior in the current event interpreter, including operand validation, timing, completion and snapshot state. Unsupported required behavior must produce an explicit failure rather than silently selecting another implementation.
- Add a player operation to its active owner and route window/queue/menu inputs through that same operation. Validate identities and quantities before committing changes.
- Add presentation through the renderer or [retained UI framework](ui-framework.md). Keep rendering state separate from the story contract and ensure clipping, viewport scaling and pointer mapping agree.
- When changing saved state, update capture, restore and validation together. Preserve source saves when an explicit migration is needed.
- Use focused contract checks for the affected layer; resource-dependent checks should use an explicitly configured installation or fixture input. See [building and testing](building-and-testing.md).

The [rendering guide](rendering.md) covers presentation extension points. Source entry points are [GameSession](../../src/Memoria.FF8.Runtime/GameSession.cs), [GameWorld](../../src/Memoria.FF8.App/GameWorld.cs), [EventEngine](../../src/Memoria.FF8.Events/EventEngine.cs) and [RecreateGame](../../src/Memoria.FF8.App/RecreateGame.cs).
