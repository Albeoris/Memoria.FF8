# Memoria FF8 documentation

Memoria is an experimental engine that reads data from your own copy of FINAL FANTASY VIII. The supported playable edition is **English Steam 2013 on Windows x64**. Resource loading for other editions does not establish playable support. See the [legal notice](../README.md#legal-notice-and-rights-holder-information) before using or distributing the project.

## Play

Start with [installation and first launch](players/getting-started.md), then use the guides below as needed.

| Guide | What you will find |
| --- | --- |
| [Controls](players/controls.md) | Keyboard, controller, remapping and shortcuts. |
| [Mod library](players/mod-library.md) | Browse Junction, download with recovery, check packages and prepare safe installations. |
| [Magic rules and Smart Attack](players/magic-system.md) | Shared stock, limited uses, automatic attacks and save compatibility. |
| [GF transformations](players/guardian-forms.md) | Optional Ifrit form, commands, HP, lifetime and finisher Skip. |
| [GF ability editor](players/gf-ability-editor.md) | Create native GF mods or save-local changes from inside the game. |
| [Localization projects](players/localization.md) | Create/convert translation projects, choose active stacks and review resource-coverage gaps. |
| [Saves](players/saves.md) | Save locations, standalone snapshots and loading limitations. |
| [Debug Room](players/debug-room.md) | Service tools, names, fixture previews and the battle laboratory. |
| [Automatic exploration](players/automatic-exploration.md) | Start, pause, resume and reproduce an experimental automated run. |

## Use mods

| Guide | What you will find |
| --- | --- |
| [Texture mods](modding/texture-mods.md) | Package discovery, manifests, adapters and supported replacements. |
| [Model browser and Blender](modding/model-mods.md) | F2 catalog, GLB exchange, skeleton warnings and model replacements. |
| [File replacements](modding/file-replacements.md) | Junction packages and the movie AVI/CAM replacement boundary. |
| [GF ability payload](modding/gf-abilities.md) | Declarative gameplay patches, guards, ordering and producer dependencies. |
| [Native field mods](modding/field-mods.md) | Scene configuration, the bundled Cid fix and Scene inspector. |
| [Translator guide](modding/localization.md) | JSON/XLSX packages, stable keys, source/context metadata, choice contracts and CLI validation. |

## Develop

| Guide | What you will find |
| --- | --- |
| [Build and test](developers/building-and-testing.md) | A clean checkout, distribution layout, test projects and optional local inputs. |
| [Cross-platform migration](developers/cross-platform.md) | Portable projects, launcher/audio contracts and remaining native host gates. |
| [Windows graphics hosts](developers/graphics-backends.md) | Shared-source DX11/Vulkan payloads, backend selection and initialization diagnostics. |
| [Shader artifacts](developers/shaders.md) | Reproducible backend profiles, manifests and shipped-payload checks. |
| [Linux testing from Windows](developers/linux-testing.md) | One-command WSL2 or SSH testing, reusable builds, Vulkan diagnostics and setup. |
| [Shared music and sound](developers/audio.md) | Common x64 SGT/DLS/PCM implementation, device ownership and validation harness. |
| [Architecture](developers/architecture.md) | Project responsibilities, state ownership and engine extension points. |
| [Chocobo World](developers/chocobo-world.md) | Integration modes, import formats, shared rewards and save compatibility. |
| [Achievements](developers/achievements.md) | Typed gameplay facts, local progress, delivery, save recovery and extension points. |
| [Battle system](developers/battle-system.md) | Combat state, lifecycle, rewards, presentation and diagnostics. |
| [Battle validation preparation](developers/battle-effects-validation.md) | Direct graphical initialization, bounded two-invocation diagnostics and remaining acceptance blockers. |
| [GF ability architecture](developers/gf-abilities.md) | Canonical definitions, identity-based progression and legacy projection. |
| [GF form architecture](developers/guardian-forms.md) | Ownership, native presentation, snapshots and the extension boundary. |
| [Settings placement](developers/settings-placement.md) | Complete setting, binding and service-tool ownership map. |
| [UI framework](developers/ui-framework.md) | Retained views, layout, input, canvas adapters and previews. |
| [Rendering](developers/rendering.md) | Rendering layers, shaders, presentation and component documentation. |
| [Remastered resource providers](developers/remastered-resources.md) | Experimental resource APIs and their compatibility limits. |
| [Localization audit](developers/localization-audit.md) | Private multilingual field comparison and cached location-name graphs; current limitations. |

## Reference

| Reference | What you will find |
| --- | --- |
| [Command line](reference/command-line.md) | Engine modes, parameters, defaults and examples. |
| [Environment variables](reference/environment-variables.md) | Runtime configuration and optional developer inputs. |
| [Control API](reference/control-api.md) | External commands, observations and graphical/headless host differences. |

Source-build examples run from the repository root: the directory containing `Build.ps1` and `Memoria.FF8.slnx`. Packaged-executable examples run from the extracted release directory, as noted on their pages. Examples use user-selected game directories and local output paths. Original game data, saves and mod payloads are supplied separately and are not distributed with these docs.
