# Native field mods

Native Memoria packages change field presentation using resources from the selected game installation. The currently supported edition is Classic. Remastered packages remain visible in the Mods menu as incompatible until their field contract is supported.

## Install and configure

Place an unpacked package in `Mods/Memoria/<package-name>`, under the selected game directory or the application mod root. The launcher selects its adjacent `Mods` directory. Direct starts from an extracted package's `Data` directory use that same package root when its launcher and `Mods` directory are present. Other direct starts use `Mods` beside the executable. `--mods-dir <directory>` selects an explicit root. The game-directory root participates in every case.

Native packages use the [shared mod enablement, ordering and refresh policy](texture-mods.md). Missing `mod.ini` inherits `auto_enable_new_mods` from `mods.ini`, which defaults to enabled. Optional `enabled = 0` or `enabled = 1` in the package's `mod.ini` overrides that policy. Game-directory copies win duplicate package IDs. Distinct packages merge their changes; the first contribution in case-insensitive alphabetical package order owns a matching background address or actor object ID.

The bundled `Depixelation` package uses the original Cid model already available in the Garden hall and hides his background image during the speech. It also hides the seated background student in the Garden classroom (`bgroom_1`), leaving the scripted departing student intact. Each fix lives in its own field JSON file. It distributes scene configuration only. Ordinary development builds copy missing bundled files beside both executables without overwriting edits. Release archives include the package under `Data/BundledMods/Memoria`, which remains compatible with existing automatic updaters. The engine discovers these defaults after the game-directory and selected application mod roots, including when `--mods-dir` selects a custom directory. A user package with the same ID supplies its configuration and overrides matching file paths; bundled defaults fill missing paths. Empty directories and settings left by an older release therefore retain access to current defaults. Ordinary game-directory versus selected-root duplicate packages still use whole-package precedence. The global policy remains in the selected root; per-package enable settings stay with the package. Launcher updates refresh untouched bundled defaults while preserving edited and pre-existing native mod files.

## Scene files

Use `fields/<field-name>.json`. Each file declares its target field ID and name, with `schemaVersion` set to `1`. JSON uses camelCase member names; unknown members and unsupported values are rejected. A package becomes usable when it supplies a valid scene patch. A README or unrelated JSON file does not make a package usable.

```json
{
  "schemaVersion": 1,
  "fieldId": 175,
  "fieldName": "bghall_6",
  "backgrounds": [
    { "layer": 0, "moduleId": 5, "state": 0, "visible": false }
  ],
  "actors": [
    {
      "objectId": 8,
      "model": { "fieldId": 175, "modelId": 9 },
      "animationId": 0,
      "when": { "moduleId": 5, "state": 0 }
    }
  ]
}
```

Background selectors identify an exact layer (0–3), module (0–255) and state (0–255). Actor entries bind an existing script object to a model and animation loaded from the installed game's field resources. An optional `direction` supplies the actor's presentation direction from 0 up to, but excluding, 256. An optional `when` restricts the presentation to an animated background module (0–254) and state (0–255). An optional background `region` object (`x`, `y`, `width`, `height`) restricts a selector to tiles whose original MAP origins lie inside that rectangle, including its left/top and excluding its right/bottom edges. Coordinates are independent of output size and texture resolution; the selector hides or shows whole tiles, so inspect their pixels before choosing a region. Origins must fit signed 16-bit coordinates and dimensions must be in 1..65536. A selector must match at least one installed tile. Background addresses including their regions, and actor object IDs, must each be unique within the file. Overlapping background selections use the first contribution for each tile. Scene inspector group overrides take precedence over mod selections. These changes affect presentation without replacing event scripts or writing installed archives.

Choose an animation that matches the object's role. Cid's model uses animation `0` for its stationary idle pose; animation `1` is a walking cycle. A presentation animation does not move the script object's position.

Reload prepares a complete new generation before publication. Malformed changes retain the last committed generation and report package/file ownership in the Mods menu. Use Refresh after correcting a file.

## Inspect a field

While a field is running, press **F2**, then select **Scene inspector** in Debug Room. Inspect the active field, background layers and script actors before writing selectors. Left/right changes categories, up/down selects entries, Confirm toggles the selected entry, and Tab moves focus through the entries, filter, Reset all and Back controls. Alternate cycles all/enabled/disabled filters; page controls navigate long lists and details. Pointer controls are also available. Temporary overrides clear on Reset all or a field change. Save lasting changes in the package's JSON file, and toggle the package in the Mods menu to compare the original and modded scene.
