# Junction file replacement packages

Junction packages provide static Data-relative replacements. The current application connects movie AVI/CAM consumers. An importer accepting an address does not establish that every asset reader consumes that overlay. Junction packages are compatible with Classic game data only.

Place a standalone `.iroj` or unpacked package directory under `Mods/Junction` in the selected game directory or beside `Memoria.Launcher.exe`. Direct application starts can select a mod root with `--mods-dir <directory>`; its `Junction` subdirectory is used. Without that option, the application uses its adjacent `Mods` directory. Both roots use the [shared mod policy, ordering and reload lifecycle](texture-mods.md). The game-directory copy wins duplicate package IDs. Archives need no manual extraction; installed game files are not overwritten.

## Manifest and configuration

The importer reads IROS versions 1.0–1.2 with uncompressed, LZS and LZMA entries. It parses root `mod.xml`, selects static `ModFolder` mappings using declared `ConfigOption` defaults and the English game language, and publishes case-insensitive Data-relative addresses. Root mappings follow the importer's Junction fallback rules. `movies/` and `lang-en/movies/` remain distinct. Metadata, previews and readme files are not replacements.

The Mods menu enables or disables the whole package; individual manifest options use declared defaults. Archive enablement lives in `<archive>.iroj.ini`; directory packages use `mod.ini`. Global policy lives in `mods.ini` under the selected mod root.

Only `FFNxConfig/enable_ffmpeg_videos` is ignored, with a diagnostic, because Memoria already uses FFmpeg. Every other FFNx setting fails import, including in disabled packages. HEXT, executable/library/plugin/helper declarations, executable payloads, dynamic game-variable conditions and unsupported manifest behavior also fail import. Rejected packages show `Reload failed`, contribute no files and leave unrelated packages available. Package code is never executed.

## Movie lifetime and timing

Uncompressed archive videos are read directly from their bounded byte range through FFmpeg's seekable `subfile` input, without extracting the movie or copying it into memory. Compressed entries, folder replacements and small CAM files are materialized only when selected. Playback pins its video and CAM together; package toggles apply to the next playback. Cached files remain available for the host's lifetime. Archive versions are checked before reopening a bound movie; changing its underlying archive requires preparing playback again.

Replacement video preparation reads container metadata without scanning or decoding the entire clip to count frames. The decoder detects the actual container, dimensions and frame rate instead of trusting the extension, and presentation follows actual decoded images and end of stream. The original installed movie retains exact decoded-frame counting for script callbacks.

Presentation frames and original movie callbacks use separate clocks. Replacement video and interpolated cameras follow detected replacement-media timing. Scripts, timers, original CAM receipts, saved frame indices and skip retain the original installed movie's timing. Replacement CAM flags must preserve the original script contract. Actor poses retain their existing update cadence. Additional actor interpolation, arbitrarily retimed videos and variable-frame-rate authoring remain outside the implemented contract.

See the [importer](../../src/Memoria.FF8.Mods/JunctionModImporter.cs), [catalog integration](../../src/Memoria.FF8.Mods/ModRuntime.Junction.cs) and [application resource boundary](../../src/Memoria.FF8.App/GameResourceHost.cs). From the product directory, run the focused package tests:

```powershell
dotnet run --project tests/Memoria.FF8.Mods.Tests
```
