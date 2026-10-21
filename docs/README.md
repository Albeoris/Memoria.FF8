# Memoria FF8 documentation

Memoria is an experimental engine that reads data from your own copy of FINAL FANTASY VIII. The supported playable edition is **English Steam 2013 on Windows x64**. Resource loading for other editions does not establish playable support. See the [legal notice](../README.md#legal-notice-and-rights-holder-information) before using or distributing the project.

## Play

Start with [installation and first launch](players/getting-started.md), then use the guides below as needed.

| Guide | What you will find |
| --- | --- |
| [Controls](players/controls.md) | Keyboard, controller, remapping and shortcuts. |
| [Magic rules and Smart Attack](players/magic-system.md) | Shared stock, limited uses, automatic attacks and save compatibility. |
| [Saves](players/saves.md) | Save locations, standalone snapshots and loading limitations. |
| [Debug Room](players/debug-room.md) | In-game settings, names, previews and the battle laboratory. |
| [Automatic exploration](players/automatic-exploration.md) | Start, pause, resume and reproduce an experimental automated run. |

## Use mods

| Guide | What you will find |
| --- | --- |
| [Texture mods](modding/texture-mods.md) | Package discovery, manifests, adapters and supported replacements. |
| [File replacements](modding/file-replacements.md) | Junction packages and the movie AVI/CAM replacement boundary. |
| [Native field mods](modding/field-mods.md) | Scene configuration, the bundled Cid fix and Scene inspector. |

## Develop

| Guide | What you will find |
| --- | --- |
| [Build and test](developers/building-and-testing.md) | A clean checkout, distribution layout, test projects and optional local inputs. |
| [Architecture](developers/architecture.md) | Project responsibilities, state ownership and engine extension points. |
| [Battle system](developers/battle-system.md) | Combat state, lifecycle, rewards, presentation and diagnostics. |
| [UI framework](developers/ui-framework.md) | Retained views, layout, input, canvas adapters and previews. |
| [Rendering](developers/rendering.md) | Rendering layers, shaders, presentation and component documentation. |
| [Remastered resource providers](developers/remastered-resources.md) | Experimental resource APIs and their compatibility limits. |

## Reference

| Reference | What you will find |
| --- | --- |
| [Command line](reference/command-line.md) | Engine modes, parameters, defaults and examples. |
| [Environment variables](reference/environment-variables.md) | Runtime configuration and optional developer inputs. |
| [Control API](reference/control-api.md) | External commands, observations and graphical/headless host differences. |

Source-build examples run from the repository root: the directory containing `Build.ps1` and `Memoria.FF8.slnx`. Packaged-executable examples run from the extracted release directory, as noted on their pages. Examples use user-selected game directories and local output paths. Original game data, saves and mod payloads are supplied separately and are not distributed with these docs.
