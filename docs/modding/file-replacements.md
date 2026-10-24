# Junction file replacement packages

Junction packages provide static resource replacements for Classic game data. The application connects movie/CAM playback, external music and the native texture layouts described in [Texture mods](texture-mods.md). An importer accepting an address does not establish that every asset reader consumes that overlay.

Place a standalone `.iroj` or unpacked package directory under `Mods/Junction` in the selected game directory or beside `Memoria.Launcher.exe`. Direct application starts can select a mod root with `--mods-dir <directory>`; its `Junction` subdirectory is used. Without that option, the application uses its adjacent `Mods` directory. Both roots use the [shared mod policy, ordering and reload lifecycle](texture-mods.md). The game-directory copy wins duplicate package IDs. Archives need no manual extraction; installed game files are not overwritten.

## Manifest and configuration

The importer reads IROS versions 1.0–1.2 with uncompressed, LZS and LZMA entries. It parses root `mod.xml`, selects static `ModFolder` mappings using declared `ConfigOption` defaults, and publishes case-insensitive resource addresses. Comparisons against undeclared static options evaluate false, matching Junction. Selected wrapper directories are stripped while resource roots such as `data/movies`, `music` and `mods/textures` retain their meaning. `movies/` and `lang-en/movies/` remain distinct. Metadata, previews and readme files are excluded unless a preview is also a selected functional resource.

The Mods menu enables or disables the whole package; individual manifest options use declared defaults. Archive enablement lives in `<archive>.iroj.ini`; directory packages use `mod.ini`. Global policy lives in `mods.ini` under the selected mod root.

Manifest GUID requirements and `Before` / `After` relationships determine package order. Missing or disabled requirements and ordering cycles produce diagnostics and prevent affected packages from contributing resources. The first matching contribution in that order wins.

Declarative `FFNxConfig` settings `enable_ffmpeg_videos`, `ffmpeg_video_ext` and `use_external_music` are understood. FFmpeg video decoding is enabled by default. Omitting `enable_ffmpeg_videos` or setting it to `1`/`true` produces no warning; an active `0`/`false` setting produces a warning because disabling FFmpeg is unsupported and decoding remains enabled. A configured video extension lets the original movie address select an MP4 or other replacement container without renaming installed resources. Other FFNx settings fail import, including in disabled packages. HEXT, executable/library/plugin/helper declarations, executable payloads, dynamic game-variable conditions and unsupported manifest behavior also fail import. Rejected packages show `Reload failed`, contribute no files and leave unrelated packages available. Package code is never executed.

## External music

Selected `music/<track>.ogg` resources replace the corresponding native track. The track name comes from the native filename after its first hyphen, without the extension. Missing replacements retain native playback. Native track identities, slots, volume changes, pauses and stop operations remain authoritative.

An optional `music/config.toml` supports global `volume` (0–100) and per-track `offset_seconds`, `intro_seconds` and `no_intro_track`. For a no-intro request, an alternate track takes precedence over the intro offset. Ogg Vorbis loop comments define the loop region. Invalid configuration fails the affected package with a diagnostic. Package changes apply when the next track starts; an already playing track retains its resources.

## Movie lifetime and timing

Uncompressed archive videos are read directly from their bounded byte range through FFmpeg's seekable `subfile` input, without extracting the movie or copying it into memory. Compressed entries, folder replacements and small CAM files are materialized only when selected. Playback pins its video and CAM together; package toggles apply to the next playback. Cached files remain available for the host's lifetime. Archive versions are checked before reopening a bound movie; changing its underlying archive requires preparing playback again.

Replacement video preparation reads container metadata without scanning or decoding the entire clip to count frames. The decoder detects the actual container, dimensions and frame rate instead of trusting the extension, and presentation follows actual decoded images and end of stream. The original installed movie retains exact decoded-frame counting for script callbacks.

Presentation frames and original movie callbacks use separate clocks. Replacement video and interpolated cameras follow detected replacement-media timing. Scripts, timers, original CAM receipts, saved frame indices and skip retain the original installed movie's timing. Replacement CAM flags must preserve the original script contract. Actor poses retain their existing update cadence. Additional actor interpolation, arbitrarily retimed videos and variable-frame-rate authoring remain outside the implemented contract.

See the [importer](../../src/Memoria.FF8.Mods/JunctionModImporter.cs), [catalog integration](../../src/Memoria.FF8.Mods/ModRuntime.Junction.cs) and [application resource boundary](../../src/Memoria.FF8.App/GameResourceHost.cs). From the product directory, run the focused package tests:

```powershell
dotnet run --project tests/Memoria.FF8.Mods.Tests
```
