# Junction file replacement packages

Junction packages provide static resource replacements for Classic game data. The application connects movie/CAM playback, external music and the native texture layouts described in [Texture mods](texture-mods.md). An importer accepting an address does not establish that every asset reader consumes that overlay.

Place a standalone `.iroj` or unpacked package directory under `Mods/Junction` in the selected game directory or beside `Memoria.Launcher.exe`. Direct application starts can select a mod root with `--mods-dir <directory>`; its `Junction` subdirectory is used. Without that option, the application uses its adjacent `Mods` directory. Both roots use the [shared mod policy, ordering and reload lifecycle](texture-mods.md). The game-directory copy wins duplicate package IDs. Archives need no manual extraction; installed game files are not overwritten. A download wrapper directory may contain one or more `.iroj` files directly inside it, alongside readme or download metadata files. Each archive is discovered as an independent package, identified by its relative path under `Junction`. A directory with its own root `mod.xml` remains an unpacked package; archives inside it are not discovered separately.

## Manifest and configuration

The importer reads IROS versions 1.0–1.2 with uncompressed, LZS and LZMA entries. It parses root `mod.xml`, selects static `ModFolder` mappings using declared `ConfigOption` defaults, and publishes case-insensitive resource addresses. Comparisons against undeclared static options evaluate false, matching Junction. Selected wrapper directories are stripped while resource roots such as `data/movies`, `music` and `mods/textures` retain their meaning. `movies/` and `lang-en/movies/` remain distinct. Metadata, previews and readme files are excluded unless a preview is also a selected functional resource.

The Mods menu enables or disables the whole package. Archive settings live in `<archive>.iroj.ini`; directory packages use `mod.ini`. Global policy lives in `mods.ini` under the selected mod root.

Static manifest options use their declared defaults. Override a declared option in the package settings with `option.<ID> = <integer>`, for example `option.texture_variant = 2` when that ID and value are declared by its manifest. Bool options accept 0 or 1; List options accept their declared values. Unknown IDs, invalid values and malformed overrides fail the owning package. Changes are reconciled through the normal watcher or Refresh; no archive or manifest edits are needed.

Manifest `Compatibility/Setting` constraints may conditionally require or forbid declared option values in another active package. Missing or disabled targets and removed target options are ignored. A conflict reports the declaring package and prevents its files from contributing; select compatible options explicitly rather than relying on automatic setting changes.

Manifest GUID requirements and `Before` / `After` relationships determine package order. Missing or disabled requirements and ordering cycles produce diagnostics and prevent affected packages from contributing resources. The first matching contribution in that order wins.

Declarative `FFNxConfig` settings `enable_ffmpeg_videos`, `ffmpeg_video_ext` and `use_external_music` are understood. FFmpeg video decoding is enabled by default. Omitting `enable_ffmpeg_videos` or setting it to `1`/`true` produces no warning; an active `0`/`false` setting produces a warning because disabling FFmpeg is unsupported and decoding remains enabled. A configured video extension lets the original movie address select an MP4 or other replacement container without renaming installed resources. Other FFNx settings fail import, including in disabled packages. HEXT, executable/library/plugin/helper declarations, executable payloads, dynamic game-variable conditions and unsupported manifest behavior also fail import. Rejected packages show `Reload failed`, contribute no files and leave unrelated packages available. Package code is never executed.

## External music

Selected `music/<track>.ogg` resources replace the corresponding native track. The track name comes from the native filename after its first hyphen, without the extension. Missing replacements retain native playback. Native track identities, slots, volume changes, pauses and stop operations remain authoritative.

An optional `music/config.toml` supports global `volume` (0–100) and per-track `offset_seconds`, `intro_seconds` and `no_intro_track`. For a no-intro request, an alternate track takes precedence over the intro offset. Ogg Vorbis loop comments define the loop region. Invalid configuration fails the affected package with a diagnostic. Package changes apply when the next track starts; an already playing track retains its resources.

## Indexed text tables

Selected `direct/exe/<table>.msd` files provide indexed text tables under the resource address `text/exe/<table>`. The current card-name consumers use `card_names`, which must contain all 110 native card identities in order. Parsing and texture preparation complete before the resource generation is published; malformed text retains the preceding generation and reports the owning package.

Inventory, refinement, reward UI and Triple Triad look up card names through the current resource generation. A native battle definition keeps the names captured when that battle was constructed; subsequent battles use the new generation. Disabling a text replacement restores the original names.

## Movie lifetime and timing

Uncompressed archive videos are read directly from their bounded byte range through FFmpeg's seekable `subfile` input, without extracting the movie or copying it into memory. Compressed entries, folder replacements and small CAM files are materialized only when selected. Playback pins its video and CAM together; package toggles apply to the next playback. Cached files remain available for the host's lifetime. Archive versions are checked before reopening a bound movie; changing its underlying archive requires preparing playback again.

Replacement video preparation reads container metadata without scanning or decoding the entire clip to count frames. The decoder detects the actual container, dimensions and frame rate instead of trusting the extension, and presentation follows actual decoded images and end of stream. The original installed movie retains exact decoded-frame counting for script callbacks.

Presentation frames and original movie callbacks use separate clocks. Replacement video and interpolated cameras follow detected replacement-media timing. Scripts, timers, original CAM receipts, saved frame indices and skip retain the original installed movie's timing. Replacement CAM flags must preserve the original script contract. Actor poses retain their existing update cadence. Additional actor interpolation, arbitrarily retimed videos and variable-frame-rate authoring remain outside the implemented contract.

For a replaced movie, its presentation CAM comes from the selected video's package. A video-only package retains the original camera; a lower-priority video's interpolated CAM is not combined with it. Interpolated camera frames can change the view between original callbacks, while model visibility continues to follow the original callback that produced the current actor samples.

See the [importer](../../src/Memoria.FF8.Mods/JunctionModImporter.cs), [catalog integration](../../src/Memoria.FF8.Mods/ModRuntime.Junction.cs) and [application resource boundary](../../src/Memoria.FF8.App/GameResourceHost.cs). From the product directory, run the focused package tests:

```powershell
dotnet run --project tests/Memoria.FF8.Mods.Tests
```
