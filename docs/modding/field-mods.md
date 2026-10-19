# Native field mods

Native Memoria packages change field presentation using resources from the selected game installation. The currently supported edition is Classic. Remastered packages remain visible in the Mods menu as incompatible until their field contract is supported.

## Install and configure

Place an unpacked package in `Mods/Memoria/<package-name>`, under the selected game directory or the application mod root. The launcher selects its adjacent `Mods` directory. Direct starts from an extracted package's `Data` directory use that same package root when its launcher and `Mods` directory are present. Other direct starts use `Mods` beside the executable. `--mods-dir <directory>` selects an explicit root. The game-directory root participates in every case.

Native packages use the [shared mod enablement, ordering and refresh policy](texture-mods.md). Missing `mod.ini` inherits `auto_enable_new_mods` from `mods.ini`, which defaults to enabled. Optional `enabled = 0` or `enabled = 1` in the package's `mod.ini` overrides that policy. Game-directory copies win duplicate package IDs. Distinct packages merge their changes; the first contribution in case-insensitive alphabetical package order owns a matching background address or actor object ID.

The bundled `Fix_CidUnpixelated` package uses the original Cid model already available in the Garden hall and hides his background image during the speech. It distributes scene configuration only. Ordinary development builds copy missing bundled files beside both executables without overwriting edits. Release archives include the package under `Mods/Memoria`. Launcher updates refresh untouched bundled defaults while preserving edited and pre-existing native mod files.

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
      "animationId": 1,
      "when": { "moduleId": 5, "state": 0 }
    }
  ]
}
```

Background selectors identify an exact layer (0–3), module (0–255) and state (0–255). Actor entries bind an existing script object to a model and animation loaded from the installed game's field resources. An optional `direction` supplies the actor's presentation direction from 0 up to, but excluding, 256. An optional `when` restricts the presentation to an animated background module (0–254) and state (0–255). Background addresses and actor object IDs must each be unique within the file. These changes affect presentation without replacing event scripts or writing installed archives.

Reload prepares a complete new generation before publication. Malformed changes retain the last committed generation and report package/file ownership in the Mods menu. Use Refresh after correcting a file.

## Inspect a field

While a field is running, press **F2**, then select **Scene inspector** in Debug Room. Inspect the active field, background layers and script actors before writing selectors. Left/right changes categories, up/down selects entries, Confirm toggles the selected entry, and Tab moves focus through the entries, filter, Reset all and Back controls. Alternate cycles all/enabled/disabled filters; page controls navigate long lists and details. Pointer controls are also available. Temporary overrides clear on Reset all or a field change. Save lasting changes in the package's JSON file, and toggle the package in the Mods menu to compare the original and modded scene.
